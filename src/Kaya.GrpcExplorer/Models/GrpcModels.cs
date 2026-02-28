namespace Kaya.GrpcExplorer.Models;

/// <summary>
/// Represents a gRPC service with its methods and metadata
/// </summary>
public class GrpcServiceInfo
{
    /// <summary>
    /// Fully qualified service name (e.g., "orders.OrderService")
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Simple service name without package (e.g., "OrderService")
    /// </summary>
    public string SimpleName { get; set; } = string.Empty;

    /// <summary>
    /// Package name (e.g., "orders")
    /// </summary>
    public string Package { get; set; } = string.Empty;

    /// <summary>
    /// Service description from proto comments
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of methods in this service
    /// </summary>
    public List<GrpcMethodInfo> Methods { get; init; } = [];
}

/// <summary>
/// Represents a gRPC method with its request/response types and streaming info
/// </summary>
public class GrpcMethodInfo
{
    /// <summary>
    /// Method name (e.g., "GetOrder")
    /// </summary>
    public string MethodName { get; init; } = string.Empty;

    /// <summary>
    /// Method type (Unary, ServerStreaming, ClientStreaming, DuplexStreaming)
    /// </summary>
    public GrpcMethodType MethodType { get; init; }

    /// <summary>
    /// Method description from proto comments
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Request message type
    /// </summary>
    public GrpcMessageSchema RequestType { get; set; } = new();

    /// <summary>
    /// Response message type
    /// </summary>
    public GrpcMessageSchema ResponseType { get; set; } = new();

    /// <summary>
    /// Whether the method is deprecated
    /// </summary>
    public bool IsDeprecated { get; set; }
}

/// <summary>
/// gRPC method types
/// </summary>
public enum GrpcMethodType
{
    /// <summary>
    /// Single request, single response
    /// </summary>
    Unary,

    /// <summary>
    /// Single request, stream of responses
    /// </summary>
    ServerStreaming,

    /// <summary>
    /// Stream of requests, single response
    /// </summary>
    ClientStreaming,

    /// <summary>
    /// Stream of requests, stream of responses
    /// </summary>
    DuplexStreaming
}

/// <summary>
/// Represents a Protobuf message schema
/// </summary>
public class GrpcMessageSchema
{
    /// <summary>
    /// Message type name (e.g., "OrderRequest")
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified type name (e.g., "orders.OrderRequest")
    /// </summary>
    public string FullTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Message description from proto comments
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// List of fields in this message
    /// </summary>
    public List<GrpcFieldInfo> Fields { get; init; } = [];

    /// <summary>
    /// Example JSON representation
    /// </summary>
    public string ExampleJson { get; set; } = "{}";
}

/// <summary>
/// Represents a field in a Protobuf message
/// </summary>
public class GrpcFieldInfo
{
    /// <summary>
    /// Field name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field number in proto definition
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Field type (e.g., "string", "int32", "OrderStatus")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this field is repeated (array)
    /// </summary>
    public bool IsRepeated { get; set; }

    /// <summary>
    /// Field description from proto comments
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Request to invoke a gRPC method
/// </summary>
public class GrpcInvocationRequest
{
    /// <summary>
    /// Server address (e.g., "localhost:5001")
    /// </summary>
    public string ServerAddress { get; init; } = string.Empty;

    /// <summary>
    /// Service name (e.g., "orders.OrderService")
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Method name (e.g., "GetOrder")
    /// </summary>
    public string MethodName { get; init; } = string.Empty;

    /// <summary>
    /// Request payload as JSON
    /// </summary>
    public string RequestJson { get; init; } = "{}";

    /// <summary>
    /// Additional metadata headers
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// For client streaming - array of request messages as JSON
    /// </summary>
    public List<string>? StreamRequests { get; init; }
}

/// <summary>
/// Request to start an interactive streaming session
/// </summary>
public class StreamStartRequest
{
    public string ServerAddress { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public Dictionary<string, string> Metadata { get; init; } = [];

    /// <summary>
    /// Used for server-streaming only: the single request message sent at stream start.
    /// For client and bidi streaming this is ignored; messages are sent via /stream/send.
    /// </summary>
    public string InitialMessageJson { get; init; } = "{}";
}

/// <summary>
/// Request to send one message into an active streaming session
/// </summary>
public class StreamSendRequest
{
    public string SessionId { get; init; } = string.Empty;
    public string MessageJson { get; init; } = "{}";
}

/// <summary>
/// Request to complete the client-side of a streaming session
/// </summary>
public class StreamEndRequest
{
    public string SessionId { get; init; } = string.Empty;
}

/// <summary>
/// Response from gRPC method invocation
/// </summary>
public class GrpcInvocationResponse
{
    /// <summary>
    /// Whether the invocation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Response payload as JSON (for unary)
    /// </summary>
    public string? ResponseJson { get; set; }

    /// <summary>
    /// Response messages as JSON (for streaming)
    /// </summary>
    public List<string>? StreamResponses { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Response metadata headers
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Status code
    /// </summary>
    public string? StatusCode { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
}
