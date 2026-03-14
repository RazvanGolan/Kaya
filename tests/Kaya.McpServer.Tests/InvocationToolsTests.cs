using System.Net;
using System.Text;
using System.Text.Json;
using Kaya.McpServer.Configuration;
using Kaya.McpServer.Core;
using Kaya.McpServer.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.McpServer.Tests;

public sealed class InvocationToolsTests
{
    private static readonly McpServerOptions Options = new(
        apiBaseUrl: "http://localhost:5121",
        grpcProxyBaseUrl: "http://localhost:5010",
        signalRDebugRoutePrefix: "/kaya-signalr");

    [Fact]
    public async Task HttpInvoke_ShouldReturnSerializedHttpResult()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });

        var tools = CreateTools(handler);

        var result = await tools.HttpInvoke("GET", "/api/health");

        using var json = JsonDocument.Parse(result);
        Assert.Equal(200, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("{\"ok\":true}", json.RootElement.GetProperty("body").GetString());
        Assert.Equal("application/json; charset=utf-8", json.RootElement.GetProperty("contentType").GetString());
    }

    [Fact]
    public async Task GrpcInvoke_ShouldUseDefaultGrpcProxyUrl()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
        });

        var tools = CreateTools(handler);

        var result = await tools.GrpcInvoke(
            serverAddress: "localhost:5000",
            serviceName: "orders.OrderService",
            methodName: "GetOrder",
            requestJson: "{\"orderId\":\"1\"}");

        Assert.Equal("{\"success\":true}", result);
        Assert.Equal("http://localhost:5010/grpc-explorer/invoke", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task SignalRHubs_ShouldUseConfiguredDefaults()
    {
        const string body = "[{\"path\":\"/chat\"}]";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

        var tools = CreateTools(handler);

        var result = await tools.SignalRHubs();

        Assert.Equal("http://localhost:5121/kaya-signalr/hubs", handler.LastRequestUri?.ToString());
        using var json = JsonDocument.Parse(result);
        Assert.Equal(200, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal(body, json.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public async Task SignalRSubscribe_WhenSessionMissing_ShouldReturnErrorString()
    {
        var result = await InvocationTools.SignalRSubscribe("missing-session", "event");

        Assert.StartsWith("ERROR:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignalRInvoke_WhenSessionMissing_ShouldReturnErrorString()
    {
        var result = await InvocationTools.SignalRInvoke("missing-session", "Send", "[]");

        Assert.StartsWith("ERROR:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignalREvents_WhenSessionMissing_ShouldReturnErrorString()
    {
        var result = await InvocationTools.SignalREvents("missing-session");

        Assert.StartsWith("ERROR:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignalRLogs_WhenSessionMissing_ShouldReturnErrorString()
    {
        var result = await InvocationTools.SignalRLogs("missing-session");

        Assert.StartsWith("ERROR:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignalRDisconnect_WhenSessionMissing_ShouldReturnNotFoundPayload()
    {
        var result = await InvocationTools.SignalRDisconnect("missing-session");

        using var json = JsonDocument.Parse(result);
        Assert.Equal("missing-session", json.RootElement.GetProperty("sessionId").GetString());
        Assert.False(json.RootElement.GetProperty("disconnected").GetBoolean());
        Assert.Equal("not-found", json.RootElement.GetProperty("reason").GetString());
    }

    private static InvocationTools CreateTools(HttpMessageHandler handler)
    {
        var factory = CreateHttpClientFactory(handler);
        return new InvocationTools(
            Options,
            new HttpInvocationService(factory),
            new GrpcInvocationService(factory),
            new SignalRInvocationService(factory));
    }

    private static IHttpClientFactory CreateHttpClientFactory(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(HttpInvocationService)).ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient(nameof(GrpcInvocationService)).ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient(nameof(SignalRInvocationService)).ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>();
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_responder(request));
        }
    }
}
