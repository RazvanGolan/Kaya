using System.Net;
using System.Text;
using System.Text.Json;
using Kaya.McpServer.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.McpServer.Tests;

public sealed class SignalRInvocationServiceTests
{
    [Fact]
    public async Task DiscoverHubsAsync_ShouldNormalizeBaseUrlAndRoute_AndReturnBody()
    {
        const string body = "[{\"path\":\"/chat\",\"hubType\":\"ChatHub\"}]";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

        var service = new SignalRInvocationService(CreateHttpClientFactory(handler));

        var response = await service.DiscoverHubsAsync(
            apiBaseUrl: "http://localhost:5121/",
            signalRDebugRoutePrefix: "kaya-signalr");

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("http://localhost:5121/kaya-signalr/hubs", handler.LastRequestUri?.ToString());

        using var json = JsonDocument.Parse(response);
        Assert.Equal(200, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal(body, json.RootElement.GetProperty("body").GetString());
    }

    private static IHttpClientFactory CreateHttpClientFactory(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(SignalRInvocationService)).ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>();
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_responder(request));
        }
    }
}
