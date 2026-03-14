using System.Text;
using System.Text.Json;
using Kaya.McpServer.Configuration;

namespace Kaya.McpServer.Core;

public sealed class GrpcInvocationService(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> InvokeAsync(
        string serverAddress,
        string serviceName,
        string methodName,
        string requestJson,
        Dictionary<string, string>? metadata,
        string grpcProxyBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var payload = new GrpcInvocationRequestDto
        {
            ServerAddress = serverAddress,
            ServiceName = serviceName,
            MethodName = methodName,
            RequestJson = string.IsNullOrWhiteSpace(requestJson) ? "{}" : requestJson,
            Metadata = metadata ?? []
        };

        return await PostJsonAsync(grpcProxyBaseUrl, "/grpc-explorer/invoke", payload, cancellationToken);
    }

    public async Task<string> StreamStartAsync(
        string serverAddress,
        string serviceName,
        string methodName,
        string initialMessageJson,
        Dictionary<string, string>? metadata,
        string grpcProxyBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var payload = new StreamStartRequestDto
        {
            ServerAddress = serverAddress,
            ServiceName = serviceName,
            MethodName = methodName,
            InitialMessageJson = string.IsNullOrWhiteSpace(initialMessageJson) ? "{}" : initialMessageJson,
            Metadata = metadata ?? []
        };

        return await PostJsonAsync(grpcProxyBaseUrl, "/grpc-explorer/stream/start", payload, cancellationToken);
    }

    public async Task StreamSendAsync(
        string sessionId,
        string messageJson,
        string grpcProxyBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var payload = new StreamSendRequestDto
        {
            SessionId = sessionId,
            MessageJson = string.IsNullOrWhiteSpace(messageJson) ? "{}" : messageJson
        };

        _ = await PostJsonAsync(grpcProxyBaseUrl, "/grpc-explorer/stream/send", payload, cancellationToken);
    }

    public async Task StreamEndAsync(
        string sessionId,
        string grpcProxyBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var payload = new StreamEndRequestDto { SessionId = sessionId };
        _ = await PostJsonAsync(grpcProxyBaseUrl, "/grpc-explorer/stream/end", payload, cancellationToken);
    }

    public async Task<List<string>> DrainStreamEventsAsync(
        string sessionId,
        int durationSeconds,
        string grpcProxyBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        var effectiveDurationSeconds = durationSeconds <= 0 ? 5 : durationSeconds;

        var baseUrl = McpServerOptions.NormalizeBaseUrl(grpcProxyBaseUrl);
        var uri = new Uri($"{baseUrl}/grpc-explorer/stream/events/{Uri.EscapeDataString(sessionId)}");

        var client = httpClientFactory.CreateClient(nameof(GrpcInvocationService));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(effectiveDurationSeconds);
        while (!reader.EndOfStream && DateTimeOffset.UtcNow < timeoutAt && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(line[5..].Trim());
            }
        }

        return result;
    }

    private async Task<string> PostJsonAsync(
        string grpcProxyBaseUrl,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var baseUrl = McpServerOptions.NormalizeBaseUrl(grpcProxyBaseUrl);
        var uri = new Uri(baseUrl + relativePath, UriKind.Absolute);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        var client = httpClientFactory.CreateClient(nameof(GrpcInvocationService));
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = (int)response.StatusCode,
                error = responseBody
            });
        }

        return responseBody;
    }

    private sealed class GrpcInvocationRequestDto
    {
        public string ServerAddress { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string RequestJson { get; set; } = "{}";
        public Dictionary<string, string> Metadata { get; set; } = [];
    }

    private sealed class StreamStartRequestDto
    {
        public string ServerAddress { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = [];
        public string InitialMessageJson { get; set; } = "{}";
    }

    private sealed class StreamSendRequestDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string MessageJson { get; set; } = "{}";
    }

    private sealed class StreamEndRequestDto
    {
        public string SessionId { get; set; } = string.Empty;
    }
}
