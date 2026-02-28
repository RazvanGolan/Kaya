using System.Collections.Concurrent;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Kaya.GrpcExplorer.Configuration;
using Kaya.GrpcExplorer.Helpers;
using Kaya.GrpcExplorer.Models;

namespace Kaya.GrpcExplorer.Services;

public interface IGrpcServiceScanner
{
    Task<List<GrpcServiceInfo>> ScanServicesAsync(string serverAddress);
    FileDescriptorSet? GetCachedDescriptorSet(string serverAddress, string serviceName);
    MethodDescriptor? GetCachedMethodDescriptor(string serverAddress, string serviceName, string methodName);
}

/// <summary>
/// Service for scanning gRPC services using Server Reflection
/// </summary>
public class GrpcServiceScanner(KayaGrpcExplorerOptions options) : IGrpcServiceScanner
{
    private readonly ConcurrentDictionary<string, List<GrpcServiceInfo>> _cache = [];
    private readonly ConcurrentDictionary<(string, string), FileDescriptorSet> _descriptorCache = [];
    // Cache method descriptors to avoid rebuilding FileDescriptors on every invocation
    private readonly ConcurrentDictionary<(string, string, string), MethodDescriptor> _methodDescriptorCache = [];

    /// <summary>
    /// Scans a gRPC server for services using reflection
    /// </summary>
    public async Task<List<GrpcServiceInfo>> ScanServicesAsync(string serverAddress)
    {
        if (_cache.TryGetValue(serverAddress, out var cachedServices))
        {
            return cachedServices;
        }

        var services = new List<GrpcServiceInfo>();

        try
        {
            var serviceNames = await GrpcReflectionHelper.ListServicesAsync(
                serverAddress,
                options.Middleware.AllowInsecureConnections);

            foreach (var serviceName in serviceNames)
            {
                try
                {
                    var serviceInfo = await GetServiceInfoAsync(serverAddress, serviceName);
                    if (serviceInfo is not null)
                    {
                        services.Add(serviceInfo);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scanning service {serviceName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning services from {serverAddress}: {ex.Message}");
        }

        _cache[serverAddress] = services;
        return services;
    }

    /// <summary>
    /// Gets detailed information about a specific service
    /// </summary>
    private async Task<GrpcServiceInfo?> GetServiceInfoAsync(string serverAddress, string serviceName)
    {
        var cacheKey = (serverAddress, serviceName);
        FileDescriptorSet? fileDescriptorSet;
        
        // Try to use cached descriptor set first
        if (_descriptorCache.TryGetValue(cacheKey, out var cachedSet))
        {
            fileDescriptorSet = cachedSet;
        }
        else
        {
            fileDescriptorSet = await GrpcReflectionHelper.GetFileDescriptorAsync(
                serverAddress,
                serviceName,
                options.Middleware.AllowInsecureConnections);
            
            if (fileDescriptorSet is not null)
            {
                _descriptorCache[cacheKey] = fileDescriptorSet;
            }
        }

        if (fileDescriptorSet is null)
        {
            return null;
        }

        // Serialize all file descriptors to ByteStrings.
        // The reflection response includes the requested file and all its transitive
        // dependencies. BuildFromByteStrings needs them all, in dependency order
        // (dependencies before dependents which is how the reflection server returns them).
        var byteStrings = new List<ByteString>();
        foreach (var fileProto in fileDescriptorSet.File)
        {
            using var ms = new MemoryStream();
            using var cos = new CodedOutputStream(ms);
            fileProto.WriteTo(cos);
            cos.Flush();
            byteStrings.Add(ByteString.CopyFrom(ms.ToArray()));
        }

        var fileDescriptors = FileDescriptor.BuildFromByteStrings(byteStrings);

        foreach (var fd in fileDescriptors)
        {
            foreach (var service in fd.Services)
            {
                if (service.FullName == serviceName)
                {
                    return BuildServiceInfo(serverAddress, service);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Builds service info from service descriptor
    /// </summary>
    private GrpcServiceInfo BuildServiceInfo(string serverAddress, ServiceDescriptor service)
    {
        var serviceInfo = new GrpcServiceInfo
        {
            ServiceName = service.FullName,
            SimpleName = service.Name,
            Package = service.File.Package,
            Description = GetLeadingComments(service),
            Methods = []
        };

        foreach (var method in service.Methods)
        {
            // Cache the method descriptor for later use during invocation
            var cacheKey = (serverAddress, service.FullName, method.Name);
            _methodDescriptorCache[cacheKey] = method;
            
            serviceInfo.Methods.Add(BuildMethodInfo(method));
        }

        return serviceInfo;
    }

    /// <summary>
    /// Builds method info from method descriptor
    /// </summary>
    private static GrpcMethodInfo BuildMethodInfo(MethodDescriptor method)
    {
        var methodType = GetMethodType(method);

        return new GrpcMethodInfo
        {
            MethodName = method.Name,
            MethodType = methodType,
            Description = GetLeadingComments(method),
            RequestType = ProtobufHelper.MessageDescriptorToSchema(method.InputType),
            ResponseType = ProtobufHelper.MessageDescriptorToSchema(method.OutputType),
            IsDeprecated = IsDeprecated(method)
        };
    }

    /// <summary>
    /// Determines the method type from descriptor
    /// </summary>
    private static GrpcMethodType GetMethodType(MethodDescriptor method)
    {
        var isClientStreaming = method.IsClientStreaming;
        var isServerStreaming = method.IsServerStreaming;

        return isClientStreaming switch
        {
            false when !isServerStreaming => GrpcMethodType.Unary,
            false when isServerStreaming => GrpcMethodType.ServerStreaming,
            true when !isServerStreaming => GrpcMethodType.ClientStreaming,
            _ => GrpcMethodType.DuplexStreaming
        };
    }

    /// <summary>
    /// Gets leading comments from descriptor
    /// </summary>
    private static string GetLeadingComments(IDescriptor descriptor)
    {
        // Source location API changed in Protobuf v4
        // Skipping comments extraction for now - can be added later
        return string.Empty;
    }

    /// <summary>
    /// Checks if a method is deprecated
    /// </summary>
    private static bool IsDeprecated(MethodDescriptor method)
    {
        return method.GetOptions()?.Deprecated ?? false;
    }

    /// <summary>
    /// Clears cache for specific server
    /// </summary>
    public void ClearCache(string serverAddress)
    {
        _cache.TryRemove(serverAddress, out _);
        
        // Remove all descriptor cache entries for this server
        var descriptorKeysToRemove = _descriptorCache.Keys
            .Where(k => k.Item1 == serverAddress)
            .ToList();
        
        foreach (var key in descriptorKeysToRemove)
        {
            _descriptorCache.TryRemove(key, out _);
        }
        
        // Remove all method descriptor cache entries for this server
        var methodKeysToRemove = _methodDescriptorCache.Keys
            .Where(k => k.Item1 == serverAddress)
            .ToList();
        
        foreach (var key in methodKeysToRemove)
        {
            _methodDescriptorCache.TryRemove(key, out _);
        }
    }
    
    /// <summary>
    /// Gets cached file descriptor set for a service (used by GrpcProxyService)
    /// </summary>
    public FileDescriptorSet? GetCachedDescriptorSet(string serverAddress, string serviceName) => 
        _descriptorCache.GetValueOrDefault((serverAddress, serviceName));

    /// <summary>
    /// Gets cached method descriptor (used by GrpcProxyService to avoid rebuilding FileDescriptors)
    /// </summary>
    public MethodDescriptor? GetCachedMethodDescriptor(string serverAddress, string serviceName, string methodName) => 
        _methodDescriptorCache.GetValueOrDefault((serverAddress, serviceName, methodName));
}
