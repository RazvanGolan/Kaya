using System.Net;
using System.Text;
using System.Text.Json;
using Kaya.McpServer.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.McpServer.Tests;

public sealed class InvocationServicesTests
{
    [Fact]
    public async Task HttpInvoke_Post_ShouldNormalizePathAndSendBodyAndHeaders()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain")
            });

        var factory = CreateHttpClientFactory(handler);
        var service = new HttpInvocationService(factory);

        var result = await service.InvokeAsync(
            method: "post",
            path: "api/orders",
            headers: new Dictionary<string, string> { ["X-Trace-Id"] = "abc-123" },
            body: "{\"id\":42}",
            baseUrl: "http://localhost:5121/");

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("http://localhost:5121/api/orders", handler.LastRequestUri?.ToString());
        Assert.Equal("{\"id\":42}", handler.LastBody);
        Assert.True(handler.LastHeaders.TryGetValue("X-Trace-Id", out var traceId));
        Assert.Equal("abc-123", traceId);

        Assert.Equal(202, result.StatusCode);
        Assert.Equal("ok", result.Body);
        Assert.Equal("text/plain; charset=utf-8", result.ContentType);
        Assert.True(result.ElapsedMs >= 0);
    }

    [Fact]
    public async Task HttpInvoke_Get_ShouldNotSendBody()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        var service = new HttpInvocationService(CreateHttpClientFactory(handler));

        await service.InvokeAsync(
            method: "GET",
            path: "/api/health",
            headers: null,
            body: "{\"ignored\":true}",
            baseUrl: "http://localhost:5000");

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Null(handler.LastBody);
    }

    [Fact]
    public async Task GrpcInvoke_ShouldDefaultEmptyRequestJsonAndMetadata()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
        });

        var service = new GrpcInvocationService(CreateHttpClientFactory(handler));

        var response = await service.InvokeAsync(
            serverAddress: "localhost:5001",
            serviceName: "orders.OrderService",
            methodName: "Create",
            requestJson: " ",
            metadata: null,
            grpcProxyBaseUrl: "http://localhost:5121/");

        Assert.Equal("{\"success\":true}", response);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("http://localhost:5121/grpc-explorer/invoke", handler.LastRequestUri?.ToString());

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("{}", doc.RootElement.GetProperty("requestJson").GetString());
        Assert.Empty(doc.RootElement.GetProperty("metadata").EnumerateObject());
    }

    [Fact]
    public async Task GrpcInvoke_WhenProxyReturnsError_ShouldReturnFailureEnvelope()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("backend exploded", Encoding.UTF8, "text/plain")
        });

        var service = new GrpcInvocationService(CreateHttpClientFactory(handler));

        var response = await service.InvokeAsync(
            serverAddress: "localhost:5001",
            serviceName: "orders.OrderService",
            methodName: "Create",
            requestJson: "{}",
            metadata: null,
            grpcProxyBaseUrl: "http://localhost:5121");

        using var doc = JsonDocument.Parse(response);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(500, doc.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("backend exploded", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task DrainStreamEvents_ShouldCollectSseDataLines()
    {
        const string sse = "event:message\n" +
                           "data: {\"count\":1}\n\n" +
                           "data: second\n\n";

        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        });

        var service = new GrpcInvocationService(CreateHttpClientFactory(handler));

        var events = await service.DrainStreamEventsAsync(
            sessionId: "session-1",
            durationSeconds: 0,
            grpcProxyBaseUrl: "http://localhost:5121");

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("http://localhost:5121/grpc-explorer/stream/events/session-1", handler.LastRequestUri?.ToString());
        Assert.Equal(2, events.Count);
        Assert.Equal("{\"count\":1}", events[0]);
        Assert.Equal("second", events[1]);
    }

    private static IHttpClientFactory CreateHttpClientFactory(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(HttpInvocationService)).ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClient(nameof(GrpcInvocationService)).ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>();
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string? LastBody { get; private set; }
        public Dictionary<string, string> LastHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;

            foreach (var header in request.Headers)
            {
                LastHeaders[header.Key] = string.Join(",", header.Value);
            }

            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    LastHeaders[header.Key] = string.Join(",", header.Value);
                }

                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            else
            {
                LastBody = null;
            }

            return _responder(request);
        }
    }
}
