using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Kaya.McpServer.Configuration;

namespace Kaya.McpServer.Core;

public sealed record HttpInvocationResult(int StatusCode, string Body, string ContentType, long ElapsedMs);

public sealed class HttpInvocationService(IHttpClientFactory httpClientFactory)
{
    public async Task<HttpInvocationResult> InvokeAsync(
        string method,
        string path,
        Dictionary<string, string>? headers,
        string? body,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var resolvedBaseUrl = McpServerOptions.NormalizeBaseUrl(baseUrl);
        var resolvedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (!resolvedPath.StartsWith('/'))
        {
            resolvedPath = "/" + resolvedPath;
        }

        var requestUri = new Uri(resolvedBaseUrl + resolvedPath, UriKind.Absolute);
        var httpMethod = new HttpMethod(method.ToUpperInvariant());

        using var request = new HttpRequestMessage(httpMethod, requestUri);

        if (!string.IsNullOrWhiteSpace(body) && httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Delete)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var added = request.Headers.TryAddWithoutValidation(key, value);
                if (!added && request.Content is not null)
                {
                    request.Content.Headers.TryAddWithoutValidation(key, value);
                }
            }
        }

        var client = httpClientFactory.CreateClient(nameof(HttpInvocationService));
        var sw = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, cancellationToken);
        sw.Stop();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        return new HttpInvocationResult((int)response.StatusCode, responseBody, contentType, sw.ElapsedMilliseconds);
    }
}
