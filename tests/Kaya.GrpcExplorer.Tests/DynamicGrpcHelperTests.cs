using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Kaya.GrpcExplorer.Helpers;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

public class DynamicGrpcHelperTests
{
    // -------------------------------------------------------------------------
    // Proto fixture helpers
    // -------------------------------------------------------------------------

    private static (FileDescriptor Descriptor, MessageDescriptor Input, MessageDescriptor Output, MethodDescriptor Method)
        BuildUnaryProto()
    {
        var fileProto = new FileDescriptorProto
        {
            Name = "dyn_test.proto",
            Package = "dyn",
            Syntax = "proto3"
        };

        var requestProto = new DescriptorProto { Name = "DynRequest" };
        requestProto.Field.Add(new FieldDescriptorProto
        {
            Name = "message",
            JsonName = "message",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.String,
            Label = FieldDescriptorProto.Types.Label.Optional
        });
        requestProto.Field.Add(new FieldDescriptorProto
        {
            Name = "count",
            JsonName = "count",
            Number = 2,
            Type = FieldDescriptorProto.Types.Type.Int32,
            Label = FieldDescriptorProto.Types.Label.Optional
        });
        requestProto.Field.Add(new FieldDescriptorProto
        {
            Name = "active",
            JsonName = "active",
            Number = 3,
            Type = FieldDescriptorProto.Types.Type.Bool,
            Label = FieldDescriptorProto.Types.Label.Optional
        });

        var responseProto = new DescriptorProto { Name = "DynResponse" };
        responseProto.Field.Add(new FieldDescriptorProto
        {
            Name = "reply",
            JsonName = "reply",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.String,
            Label = FieldDescriptorProto.Types.Label.Optional
        });

        var serviceProto = new ServiceDescriptorProto { Name = "DynService" };
        serviceProto.Method.Add(new MethodDescriptorProto
        {
            Name = "Echo",
            InputType = ".dyn.DynRequest",
            OutputType = ".dyn.DynResponse",
            ClientStreaming = false,
            ServerStreaming = false
        });
        serviceProto.Method.Add(new MethodDescriptorProto
        {
            Name = "ServerStream",
            InputType = ".dyn.DynRequest",
            OutputType = ".dyn.DynResponse",
            ClientStreaming = false,
            ServerStreaming = true
        });
        serviceProto.Method.Add(new MethodDescriptorProto
        {
            Name = "ClientStream",
            InputType = ".dyn.DynRequest",
            OutputType = ".dyn.DynResponse",
            ClientStreaming = true,
            ServerStreaming = false
        });
        serviceProto.Method.Add(new MethodDescriptorProto
        {
            Name = "Duplex",
            InputType = ".dyn.DynRequest",
            OutputType = ".dyn.DynResponse",
            ClientStreaming = true,
            ServerStreaming = true
        });

        fileProto.MessageType.Add(requestProto);
        fileProto.MessageType.Add(responseProto);
        fileProto.Service.Add(serviceProto);

        var fd = BuildFileDescriptor(fileProto);
        return (fd, fd.MessageTypes[0], fd.MessageTypes[1], fd.Services[0].Methods[0]);
    }

    private static FileDescriptor BuildFileDescriptor(FileDescriptorProto fileProto)
    {
        using var ms = new MemoryStream();
        using var cos = new CodedOutputStream(ms);
        fileProto.WriteTo(cos);
        cos.Flush();
        return FileDescriptor.BuildFromByteStrings([ByteString.CopyFrom(ms.ToArray())])[0];
    }

    // -------------------------------------------------------------------------
    // CreateMessageFromJson
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateMessageFromJson_ShouldReturnMessage_WithStringField()
    {
        var (_, input, _, _) = BuildUnaryProto();

        var message = DynamicGrpcHelper.CreateMessageFromJson(input, "{\"message\": \"hello\"}");

        message.Should().NotBeNull();
    }

    [Fact]
    public void CreateMessageFromJson_ShouldReturnMessage_WithEmptyJson()
    {
        var (_, input, _, _) = BuildUnaryProto();

        var message = DynamicGrpcHelper.CreateMessageFromJson(input, "{}");

        message.Should().NotBeNull();
    }

    [Fact]
    public void CreateMessageFromJson_ShouldReturnMessage_WithMultipleFields()
    {
        var (_, input, _, _) = BuildUnaryProto();

        var message = DynamicGrpcHelper.CreateMessageFromJson(
            input, "{\"message\": \"test\", \"count\": 42, \"active\": true}");

        message.Should().NotBeNull();
    }

    [Fact]
    public void CreateMessageFromJson_ShouldThrow_ForInvalidJson()
    {
        var (_, input, _, _) = BuildUnaryProto();

        var act = () => DynamicGrpcHelper.CreateMessageFromJson(input, "not-valid-json");

        act.Should().Throw<Exception>();
    }

    // -------------------------------------------------------------------------
    // MessageToJson
    // -------------------------------------------------------------------------

    [Fact]
    public void MessageToJson_ShouldReturnJsonString_ForDynamicMessage()
    {
        var (_, input, _, _) = BuildUnaryProto();
        var message = DynamicGrpcHelper.CreateMessageFromJson(input, "{\"message\": \"hello\"}");

        var json = DynamicGrpcHelper.MessageToJson(message);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("message");
        json.Should().Contain("hello");
    }

    [Fact]
    public void MessageToJson_ShouldReturnEmptyish_ForEmptyMessage()
    {
        var (_, input, _, _) = BuildUnaryProto();
        var message = DynamicGrpcHelper.CreateMessageFromJson(input, "{}");

        var json = DynamicGrpcHelper.MessageToJson(message);

        json.Should().NotBeNull();
    }

    [Fact]
    public void MessageToJson_ShouldRoundTrip_StringField()
    {
        var (_, input, _, _) = BuildUnaryProto();
        var message = DynamicGrpcHelper.CreateMessageFromJson(input, "{\"message\": \"roundtrip\"}");

        var json = DynamicGrpcHelper.MessageToJson(message);

        json.Should().Contain("roundtrip");
    }

    // -------------------------------------------------------------------------
    // CreateMethod
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateMethod_ShouldReturnUnaryMethod_ForUnaryDescriptor()
    {
        var (fd, _, _, method) = BuildUnaryProto();

        var grpcMethod = DynamicGrpcHelper.CreateMethod(method, "dyn.DynService");

        grpcMethod.Type.Should().Be(MethodType.Unary);
        grpcMethod.Name.Should().Be("Echo");
        grpcMethod.ServiceName.Should().Be("dyn.DynService");
    }

    [Fact]
    public void CreateMethod_ShouldReturnServerStreamingMethod_ForServerStreamingDescriptor()
    {
        var fd = BuildUnaryProto().Descriptor;

        var serverStreamMethod = fd.Services[0].Methods[1]; // "ServerStream"
        var grpcMethod = DynamicGrpcHelper.CreateMethod(serverStreamMethod, "dyn.DynService");

        grpcMethod.Type.Should().Be(MethodType.ServerStreaming);
        grpcMethod.Name.Should().Be("ServerStream");
    }

    [Fact]
    public void CreateMethod_ShouldReturnClientStreamingMethod_ForClientStreamingDescriptor()
    {
        var fd = BuildUnaryProto().Descriptor;

        var clientStreamMethod = fd.Services[0].Methods[2]; // "ClientStream"
        var grpcMethod = DynamicGrpcHelper.CreateMethod(clientStreamMethod, "dyn.DynService");

        grpcMethod.Type.Should().Be(MethodType.ClientStreaming);
    }

    [Fact]
    public void CreateMethod_ShouldReturnDuplexStreamingMethod_ForDuplexDescriptor()
    {
        var fd = BuildUnaryProto().Descriptor;

        var duplexMethod = fd.Services[0].Methods[3]; // "Duplex"
        var grpcMethod = DynamicGrpcHelper.CreateMethod(duplexMethod, "dyn.DynService");

        grpcMethod.Type.Should().Be(MethodType.DuplexStreaming);
    }

    [Fact]
    public void CreateMethod_RequestMarshallerSerializer_ShouldSerializeMessage()
    {
        var (_, input, _, method) = BuildUnaryProto();
        var grpcMethod = DynamicGrpcHelper.CreateMethod(method, "dyn.DynService");

        var message = DynamicGrpcHelper.CreateMessageFromJson(input, "{\"message\": \"test\"}");
        var bytes = grpcMethod.RequestMarshaller.Serializer(message);

        bytes.Should().NotBeNull();
    }

    [Fact]
    public void CreateMethod_RequestMarshallerDeserializer_ShouldDeserializeBytes()
    {
        var (_, input, _, method) = BuildUnaryProto();
        var grpcMethod = DynamicGrpcHelper.CreateMethod(method, "dyn.DynService");

        var message = DynamicGrpcHelper.CreateMessageFromJson(input, "{\"message\": \"test\"}");
        var bytes = grpcMethod.RequestMarshaller.Serializer(message);
        var deserialized = grpcMethod.RequestMarshaller.Deserializer(bytes);

        deserialized.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // GetMethodDescriptorAsync (with pre-built FileDescriptorSet — no network)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMethodDescriptorAsync_ShouldReturnDescriptor_WhenCachedSetContainsMethod()
    {
        var fileProto = new FileDescriptorProto
        {
            Name = "cached.proto",
            Package = "cached",
            Syntax = "proto3"
        };
        var msgProto = new DescriptorProto { Name = "Req" };
        msgProto.Field.Add(new FieldDescriptorProto
        {
            Name = "id",
            JsonName = "id",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.Int32,
            Label = FieldDescriptorProto.Types.Label.Optional
        });
        var svcProto = new ServiceDescriptorProto { Name = "CachedSvc" };
        svcProto.Method.Add(new MethodDescriptorProto
        {
            Name = "DoIt",
            InputType = ".cached.Req",
            OutputType = ".cached.Req"
        });
        fileProto.MessageType.Add(msgProto);
        fileProto.Service.Add(svcProto);

        var fdSet = new FileDescriptorSet();
        fdSet.File.Add(fileProto);

        var descriptor = await DynamicGrpcHelper.GetMethodDescriptorAsync(
            "localhost:9999", "cached.CachedSvc", "DoIt", false, fdSet);

        descriptor.Should().NotBeNull();
        descriptor!.Name.Should().Be("DoIt");
    }

    [Fact]
    public async Task GetMethodDescriptorAsync_ShouldReturnNull_WhenMethodNameNotFound()
    {
        var fileProto = new FileDescriptorProto
        {
            Name = "nf.proto",
            Package = "nf",
            Syntax = "proto3"
        };
        var msgProto = new DescriptorProto { Name = "Req" };
        var svcProto = new ServiceDescriptorProto { Name = "NfSvc" };
        svcProto.Method.Add(new MethodDescriptorProto
        {
            Name = "RealMethod",
            InputType = ".nf.Req",
            OutputType = ".nf.Req"
        });
        fileProto.MessageType.Add(msgProto);
        fileProto.Service.Add(svcProto);

        var fdSet = new FileDescriptorSet();
        fdSet.File.Add(fileProto);

        var descriptor = await DynamicGrpcHelper.GetMethodDescriptorAsync(
            "localhost:9999", "nf.NfSvc", "GhostMethod", false, fdSet);

        descriptor.Should().BeNull();
    }

    [Fact]
    public async Task GetMethodDescriptorAsync_ShouldThrow_WhenDescriptorSetIsNullAndServerUnreachable()
    {
        // When no cached descriptor set is provided and the server is unreachable,
        // GrpcReflectionHelper.GetFileDescriptorAsync propagates a network RpcException.
        var act = async () => await DynamicGrpcHelper.GetMethodDescriptorAsync(
            "localhost:9", "some.Svc", "Method", true, null);

        await act.Should().ThrowAsync<Exception>();
    }
}
