using FluentAssertions;
using Kaya.GrpcExplorer.Models;
using Kaya.GrpcExplorer.Services;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

public class ModelsTests
{
    [Fact]
    public void GrpcServiceInfo_ShouldHaveDefaultValues()
    {
        var info = new GrpcServiceInfo();

        info.ServiceName.Should().Be(string.Empty);
        info.SimpleName.Should().Be(string.Empty);
        info.Package.Should().Be(string.Empty);
        info.Description.Should().Be(string.Empty);
        info.Methods.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void GrpcServiceInfo_ShouldAllowPropertySetting()
    {
        var info = new GrpcServiceInfo
        {
            ServiceName = "orders.OrderService",
            SimpleName = "OrderService",
            Package = "orders",
            Description = "Order management service"
        };
        info.Methods.Add(new GrpcMethodInfo { MethodName = "GetOrder" });

        info.ServiceName.Should().Be("orders.OrderService");
        info.SimpleName.Should().Be("OrderService");
        info.Package.Should().Be("orders");
        info.Description.Should().Be("Order management service");
        info.Methods.Should().HaveCount(1);
    }

    [Fact]
    public void GrpcMethodInfo_ShouldHaveDefaultValues()
    {
        var info = new GrpcMethodInfo();

        info.MethodName.Should().Be(string.Empty);
        info.MethodType.Should().Be(GrpcMethodType.Unary);
        info.Description.Should().Be(string.Empty);
        info.RequestType.Should().NotBeNull();
        info.ResponseType.Should().NotBeNull();
        info.IsDeprecated.Should().BeFalse();
    }

    [Theory]
    [InlineData(GrpcMethodType.Unary)]
    [InlineData(GrpcMethodType.ServerStreaming)]
    [InlineData(GrpcMethodType.ClientStreaming)]
    [InlineData(GrpcMethodType.DuplexStreaming)]
    public void GrpcMethodType_ShouldSupportAllValues(GrpcMethodType methodType)
    {
        var info = new GrpcMethodInfo { MethodType = methodType };
        info.MethodType.Should().Be(methodType);
    }

    [Fact]
    public void GrpcMessageSchema_ShouldHaveDefaultValues()
    {
        var schema = new GrpcMessageSchema();

        schema.TypeName.Should().Be(string.Empty);
        schema.FullTypeName.Should().Be(string.Empty);
        schema.Description.Should().Be(string.Empty);
        schema.Fields.Should().NotBeNull().And.BeEmpty();
        schema.ExampleJson.Should().Be("{}");
    }

    [Fact]
    public void GrpcMessageSchema_ShouldAllowPropertySetting()
    {
        var schema = new GrpcMessageSchema
        {
            TypeName = "OrderRequest",
            FullTypeName = "orders.OrderRequest",
            ExampleJson = "{\"id\": 1}"
        };
        schema.Fields.Add(new GrpcFieldInfo { Name = "id", Number = 1, Type = "int32" });

        schema.TypeName.Should().Be("OrderRequest");
        schema.FullTypeName.Should().Be("orders.OrderRequest");
        schema.ExampleJson.Should().Be("{\"id\": 1}");
        schema.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void GrpcFieldInfo_ShouldHaveDefaultValues()
    {
        var field = new GrpcFieldInfo();

        field.Name.Should().Be(string.Empty);
        field.Number.Should().Be(0);
        field.Type.Should().Be(string.Empty);
        field.IsRepeated.Should().BeFalse();
        field.Description.Should().Be(string.Empty);
    }

    [Fact]
    public void GrpcFieldInfo_ShouldAllowPropertySetting()
    {
        var field = new GrpcFieldInfo
        {
            Name = "tags",
            Number = 3,
            Type = "string",
            IsRepeated = true,
            Description = "List of tags"
        };

        field.Name.Should().Be("tags");
        field.Number.Should().Be(3);
        field.Type.Should().Be("string");
        field.IsRepeated.Should().BeTrue();
        field.Description.Should().Be("List of tags");
    }

    [Fact]
    public void GrpcInvocationRequest_ShouldHaveDefaultValues()
    {
        var request = new GrpcInvocationRequest();

        request.ServerAddress.Should().Be(string.Empty);
        request.ServiceName.Should().Be(string.Empty);
        request.MethodName.Should().Be(string.Empty);
        request.RequestJson.Should().Be("{}");
        request.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void GrpcInvocationRequest_ShouldAllowInitialization()
    {
        var request = new GrpcInvocationRequest
        {
            ServerAddress = "localhost:5001",
            ServiceName = "orders.OrderService",
            MethodName = "GetOrder",
            RequestJson = "{\"id\": 42}",
            Metadata = new Dictionary<string, string> { { "Authorization", "Bearer token" } }
        };

        request.ServerAddress.Should().Be("localhost:5001");
        request.ServiceName.Should().Be("orders.OrderService");
        request.MethodName.Should().Be("GetOrder");
        request.RequestJson.Should().Be("{\"id\": 42}");
        request.Metadata.Should().ContainKey("Authorization");
    }

    [Fact]
    public void GrpcInvocationResponse_ShouldHaveDefaultValues()
    {
        var response = new GrpcInvocationResponse();

        response.Success.Should().BeFalse();
        response.ResponseJson.Should().BeNull();
        response.ErrorMessage.Should().BeNull();
        response.Metadata.Should().NotBeNull().And.BeEmpty();
        response.StatusCode.Should().BeNull();
        response.DurationMs.Should().Be(0);
    }

    [Fact]
    public void GrpcInvocationResponse_ShouldAllowMutation()
    {
        var response = new GrpcInvocationResponse { Success = true, StatusCode = "OK" };
        response.DurationMs = 150;
        response.ResponseJson = "{\"result\": 1}";
        response.Metadata["key"] = "val";

        response.Success.Should().BeTrue();
        response.StatusCode.Should().Be("OK");
        response.DurationMs.Should().Be(150);
        response.ResponseJson.Should().Be("{\"result\": 1}");
        response.Metadata.Should().ContainKey("key").WhoseValue.Should().Be("val");
    }

    [Fact]
    public void GrpcInvocationResponse_ErrorResponse_ShouldHaveErrorFields()
    {
        var response = new GrpcInvocationResponse
        {
            Success = false,
            ErrorMessage = "Service not found",
            StatusCode = "NOT_FOUND",
            DurationMs = 5
        };

        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Be("Service not found");
        response.StatusCode.Should().Be("NOT_FOUND");
        response.DurationMs.Should().Be(5);
    }

    [Fact]
    public void StreamStartRequest_ShouldHaveDefaultValues()
    {
        var req = new StreamStartRequest();

        req.ServerAddress.Should().Be(string.Empty);
        req.ServiceName.Should().Be(string.Empty);
        req.MethodName.Should().Be(string.Empty);
        req.Metadata.Should().NotBeNull().And.BeEmpty();
        req.InitialMessageJson.Should().Be("{}");
    }

    [Fact]
    public void StreamStartRequest_ShouldAllowInitialization()
    {
        var req = new StreamStartRequest
        {
            ServerAddress = "localhost:5001",
            ServiceName = "orders.OrderService",
            MethodName = "StreamOrders",
            InitialMessageJson = "{\"filter\": \"all\"}",
            Metadata = new Dictionary<string, string> { { "x-token", "abc" } }
        };

        req.ServerAddress.Should().Be("localhost:5001");
        req.MethodName.Should().Be("StreamOrders");
        req.InitialMessageJson.Should().Be("{\"filter\": \"all\"}");
        req.Metadata.Should().ContainKey("x-token");
    }

    [Fact]
    public void StreamSendRequest_ShouldHaveDefaultValues()
    {
        var req = new StreamSendRequest();

        req.SessionId.Should().Be(string.Empty);
        req.MessageJson.Should().Be("{}");
    }

    [Fact]
    public void StreamSendRequest_ShouldAllowInitialization()
    {
        var req = new StreamSendRequest { SessionId = "abc123", MessageJson = "{\"data\": 1}" };

        req.SessionId.Should().Be("abc123");
        req.MessageJson.Should().Be("{\"data\": 1}");
    }

    [Fact]
    public void StreamEndRequest_ShouldHaveDefaultValues()
    {
        var req = new StreamEndRequest();
        req.SessionId.Should().Be(string.Empty);
    }

    [Fact]
    public void StreamEndRequest_ShouldAllowInitialization()
    {
        var req = new StreamEndRequest { SessionId = "sess-42" };
        req.SessionId.Should().Be("sess-42");
    }

    [Theory]
    [InlineData(SseEventType.Message, "some payload")]
    [InlineData(SseEventType.Complete, "stream complete")]
    [InlineData(SseEventType.Error, "error detail")]
    public void SseEvent_ShouldStoreTypeAndPayload(SseEventType type, string payload)
    {
        var evt = new SseEvent(type, payload);

        evt.Type.Should().Be(type);
        evt.Payload.Should().Be(payload);
    }

    [Fact]
    public void SseEvent_ShouldSupportValueEquality()
    {
        var a = new SseEvent(SseEventType.Message, "hello");
        var b = new SseEvent(SseEventType.Message, "hello");
        var c = new SseEvent(SseEventType.Error, "hello");

        a.Should().Be(b);
        a.Should().NotBe(c);
    }
}
