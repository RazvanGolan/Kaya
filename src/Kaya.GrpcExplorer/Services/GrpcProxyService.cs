using System.Diagnostics;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Kaya.GrpcExplorer.Configuration;
using Kaya.GrpcExplorer.Helpers;
using Kaya.GrpcExplorer.Models;

namespace Kaya.GrpcExplorer.Services;

public interface IGrpcProxyService
{
   Task<GrpcInvocationResponse> InvokeMethodAsync(GrpcInvocationRequest request);

   /// <summary>Starts an interactive streaming session and returns the session ID.</summary>
   Task<string> StartStreamAsync(StreamStartRequest request);

   /// <summary>Sends one message into an active streaming session.</summary>
   Task SendMessageAsync(StreamSendRequest request);

   /// <summary>Completes the client-side request stream of an active session.</summary>
   Task EndStreamAsync(StreamEndRequest request);
}

/// <summary>
/// Service for proxying gRPC method invocations
/// </summary>
public class GrpcProxyService(
    KayaGrpcExplorerOptions options,
    IGrpcServiceScanner scanner,
    IStreamingSessionManager sessionManager) : IGrpcProxyService
{

    /// <summary>
    /// Invokes a gRPC method and returns the response
    /// </summary>
    public async Task<GrpcInvocationResponse> InvokeMethodAsync(GrpcInvocationRequest request)
   {
       var stopwatch = Stopwatch.StartNew();

       try
       {
           // Get service info
           var services = await scanner.ScanServicesAsync(request.ServerAddress);
           var service = services.FirstOrDefault(s => s.ServiceName == request.ServiceName);
           
           if (service is null)
           {
               return new GrpcInvocationResponse
               {
                   Success = false,
                   ErrorMessage = $"Service '{request.ServiceName} ' not found"
               };
           }

           var method = service.Methods.FirstOrDefault(m => m.MethodName == request.MethodName);
           if (method is null)
           {
               return new GrpcInvocationResponse
               {
                   Success = false,
                   ErrorMessage = $"Method '{request.MethodName}' not found"
               };
           }

           // Get or create channel (reuse existing connection from shared cache)
           var channel = GrpcReflectionHelper.GetOrCreateChannel(
               request.ServerAddress,
               options.Middleware.AllowInsecureConnections);

           // Create metadata
           var metadata = GrpcReflectionHelper.CreateMetadata(request.Metadata);

           // Invoke based on method type
           var response = method.MethodType switch
           {
               GrpcMethodType.Unary => await InvokeUnaryAsync(channel, request.ServiceName, method, request.RequestJson, metadata),
               _ => throw new NotSupportedException($"Method type {method.MethodType} is not supported via /invoke. Use the streaming SSE endpoints instead.")
           };

           stopwatch.Stop();
           response.DurationMs = stopwatch.ElapsedMilliseconds;
           return response;
       }
       catch (RpcException rpcEx)
       {
           stopwatch.Stop();
           return new GrpcInvocationResponse
           {
               Success = false,
               ErrorMessage = $"gRPC Error: {rpcEx.Status.Detail}",
               StatusCode = rpcEx.StatusCode.ToString(),
               DurationMs = stopwatch.ElapsedMilliseconds
           };
       }
       catch (Exception ex)
       {
           stopwatch.Stop();
           return new GrpcInvocationResponse
           {
               Success = false,
               ErrorMessage = ex.Message,
               DurationMs = stopwatch.ElapsedMilliseconds
           };
       }
   }

   /// <summary>
   /// Invokes a unary gRPC method
   /// </summary>
   private async Task<GrpcInvocationResponse> InvokeUnaryAsync(
       GrpcChannel channel,
       string serviceName,
       GrpcMethodInfo method,
       string requestJson,
       Metadata metadata)
   {
       var methodDescriptor = await GetMethodDescriptorByName(channel.Target, serviceName, method.MethodName);
       if (methodDescriptor is null)
       {
           return new GrpcInvocationResponse
           {
               Success = false,
               ErrorMessage = "Could not find method descriptor"
           };
       }

       var request = DynamicGrpcHelper.CreateMessageFromJson(methodDescriptor.InputType, requestJson);

       var grpcMethod = DynamicGrpcHelper.CreateMethod(methodDescriptor, methodDescriptor.Service.FullName);

       var response = await DynamicGrpcHelper.InvokeUnaryAsync(channel, grpcMethod, request, metadata);

       var responseJson = DynamicGrpcHelper.MessageToJson(response);

       return new GrpcInvocationResponse
       {
           Success = true,
           ResponseJson = responseJson,
           StatusCode = "OK"
       };
   }

   // -------------------------------------------------------------------------
   // Interactive streaming — session-based
   // -------------------------------------------------------------------------

   /// <inheritdoc/>
   public async Task<string> StartStreamAsync(StreamStartRequest request)
   {
       var channel = GrpcReflectionHelper.GetOrCreateChannel(
           request.ServerAddress,
           options.Middleware.AllowInsecureConnections);

       var methodDescriptor = await GetMethodDescriptorByName(
           request.ServerAddress, request.ServiceName, request.MethodName) ?? throw new InvalidOperationException($"Method '{request.MethodName}' not found on '{request.ServiceName}'.");
       var metadata = GrpcReflectionHelper.CreateMetadata(request.Metadata);
       var grpcMethod = DynamicGrpcHelper.CreateMethod(methodDescriptor, methodDescriptor.Service.FullName);

       var session = new StreamingSession { MethodDescriptor = methodDescriptor };

       switch (grpcMethod.Type)
       {
           case MethodType.ServerStreaming:
           {
               var call = channel.CreateCallInvoker().AsyncServerStreamingCall(
                   grpcMethod, null, new CallOptions(headers: metadata),
                   DynamicGrpcHelper.CreateMessageFromJson(methodDescriptor.InputType, request.InitialMessageJson));

               session.ResponseReaderTask = ReadServerStreamAsync(session, call.ResponseStream, session.Cts.Token);
               break;
           }

           case MethodType.ClientStreaming:
           {
               var call = channel.CreateCallInvoker().AsyncClientStreamingCall(
                   grpcMethod, null, new CallOptions(headers: metadata));

               var writer = call.RequestStream;
               session = new StreamingSession
               {
                   MethodDescriptor = methodDescriptor,
                   RequestWriter = writer
               };

               // When the request stream completes, await the single response and push it as an SSE event
               session.ResponseReaderTask = AwaitClientStreamResponseAsync(session, call, session.Cts.Token);
               break;
           }

           case MethodType.DuplexStreaming:
           {
               var call = channel.CreateCallInvoker().AsyncDuplexStreamingCall(
                   grpcMethod, null, new CallOptions(headers: metadata));

               session = new StreamingSession
               {
                   MethodDescriptor = methodDescriptor,
                   RequestWriter = call.RequestStream
               };

               session.ResponseReaderTask = ReadDuplexStreamAsync(session, call.ResponseStream, session.Cts.Token);
               break;
           }

           default:
               throw new InvalidOperationException("StartStreamAsync is only for streaming methods.");
       }

       sessionManager.Add(session);
       return session.Id;
   }

   /// <inheritdoc/>
   public async Task SendMessageAsync(StreamSendRequest request)
   {
       var session = sessionManager.Get(request.SessionId)
           ?? throw new InvalidOperationException($"Session '{request.SessionId}' not found.");

       if (session.RequestWriter is null)
           throw new InvalidOperationException("This session does not accept client messages (server-streaming only).");

       var message = DynamicGrpcHelper.CreateMessageFromJson(session.MethodDescriptor.InputType, request.MessageJson);
       await session.RequestWriter.WriteAsync(message, session.Cts.Token);
   }

   /// <inheritdoc/>
   public async Task EndStreamAsync(StreamEndRequest request)
   {
       var session = sessionManager.Get(request.SessionId)
           ?? throw new InvalidOperationException($"Session '{request.SessionId}' not found.");

       if (session.RequestWriter is null)
           throw new InvalidOperationException("This session does not accept client messages (server-streaming only).");

       await session.RequestWriter.CompleteAsync();
   }

   // -------------------------------------------------------------------------
   // Background reader helpers
   // -------------------------------------------------------------------------

   private static async Task ReadServerStreamAsync(
       StreamingSession session,
       IAsyncStreamReader<IMessage> responseStream,
       CancellationToken ct)
   {
       try
       {
           await foreach (var msg in responseStream.ReadAllAsync(ct))
           {
               var json = DynamicGrpcHelper.MessageToJson(msg);
               session.EventWriter.TryWrite(new SseEvent(SseEventType.Message, json));
           }
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Complete, "stream complete"));
       }
       catch (OperationCanceledException) { /* client disconnected */ }
       catch (RpcException rex)
       {
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Error, rex.Status.Detail));
       }
       catch (Exception ex)
       {
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Error, ex.Message));
       }
       finally
       {
           session.EventWriter.TryComplete();
       }
   }

   private static async Task ReadDuplexStreamAsync(
       StreamingSession session,
       IAsyncStreamReader<IMessage> responseStream,
       CancellationToken ct)
   {
       try
       {
           await foreach (var msg in responseStream.ReadAllAsync(ct))
           {
               var json = DynamicGrpcHelper.MessageToJson(msg);
               session.EventWriter.TryWrite(new SseEvent(SseEventType.Message, json));
           }
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Complete, "stream complete"));
       }
       catch (OperationCanceledException) { /* client disconnected */ }
       catch (RpcException rex)
       {
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Error, rex.Status.Detail));
       }
       catch (Exception ex)
       {
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Error, ex.Message));
       }
       finally
       {
           session.EventWriter.TryComplete();
       }
   }

   private static async Task AwaitClientStreamResponseAsync(
       StreamingSession session,
       AsyncClientStreamingCall<IMessage, IMessage> call,
       CancellationToken ct)
   {
       try
       {
           // Block until the caller invokes EndStreamAsync (CompleteAsync on the request stream)
           var response = await call.ResponseAsync.WaitAsync(ct);
           var json = DynamicGrpcHelper.MessageToJson(response);
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Message, json));
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Complete, "stream complete"));
       }
       catch (OperationCanceledException) { /* client disconnected */ }
       catch (RpcException rex)
       {
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Error, rex.Status.Detail));
       }
       catch (Exception ex)
       {
           session.EventWriter.TryWrite(new SseEvent(SseEventType.Error, ex.Message));
       }
       finally
       {
           session.EventWriter.TryComplete();
       }
   }

   // -------------------------------------------------------------------------

   /// <summary>
   /// Looks up a method descriptor directly by service + method name (used by interactive streaming)
   /// </summary>
   private async Task<Google.Protobuf.Reflection.MethodDescriptor?> GetMethodDescriptorByName(
       string serverAddress, string serviceName, string methodName)
   {
       var cachedDescriptor = scanner.GetCachedMethodDescriptor(serverAddress, serviceName, methodName);
       if (cachedDescriptor is not null)
           return cachedDescriptor;

       var cachedDescriptorSet = scanner.GetCachedDescriptorSet(serverAddress, serviceName);
       return await DynamicGrpcHelper.GetMethodDescriptorAsync(
           serverAddress, serviceName, methodName,
           options.Middleware.AllowInsecureConnections,
           cachedDescriptorSet);
   }

}
