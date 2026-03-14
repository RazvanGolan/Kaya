using System.Collections.Concurrent;
using System.Text.Json;
using Kaya.McpServer.Configuration;
using Microsoft.AspNetCore.SignalR.Client;

namespace Kaya.McpServer.Core;

public sealed class SignalRInvocationService(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SignalRSession> Sessions = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> DiscoverHubsAsync(
        string apiBaseUrl,
        string signalRDebugRoutePrefix,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = McpServerOptions.NormalizeBaseUrl(apiBaseUrl);
        var route = McpServerOptions.NormalizePath(signalRDebugRoutePrefix);
        var url = $"{baseUrl}{route}/hubs";

        var client = httpClientFactory.CreateClient(nameof(SignalRInvocationService));
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            statusCode = (int)response.StatusCode,
            body
        }, JsonOptions);
    }

    public static async Task<string> ConnectAsync(
        string hubPath,
        string signalRBaseUrl,
        Dictionary<string, string>? headers,
        string? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = McpServerOptions.NormalizeBaseUrl(signalRBaseUrl);
        var normalizedHubPath = McpServerOptions.NormalizePath(hubPath);
        var url = $"{baseUrl}{normalizedHubPath}";

        var sessionId = string.IsNullOrWhiteSpace(connectionId)
            ? Guid.NewGuid().ToString("N")
            : connectionId.Trim();

        if (Sessions.ContainsKey(sessionId))
        {
            throw new InvalidOperationException($"Session '{sessionId}' already exists.");
        }

        var builder = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                if (headers is null)
                {
                    return;
                }

                foreach (var (key, value) in headers)
                {
                    options.Headers[key] = value;
                }
            })
            .WithAutomaticReconnect();

        var connection = builder.Build();
        var session = new SignalRSession(sessionId, url, connection);

        connection.Closed += ex =>
        {
            session.AddLog("closed", ex?.Message ?? "Connection closed");
            return Task.CompletedTask;
        };

        connection.Reconnecting += ex =>
        {
            session.AddLog("reconnecting", ex?.Message ?? "Reconnecting");
            return Task.CompletedTask;
        };

        connection.Reconnected += newConnId =>
        {
            session.AddLog("reconnected", $"ConnectionId={newConnId ?? "n/a"}");
            return Task.CompletedTask;
        };

        Sessions[sessionId] = session;

        try
        {
            await connection.StartAsync(cancellationToken);
            session.AddLog("connected", "SignalR connection started");

            return JsonSerializer.Serialize(new
            {
                sessionId,
                hubUrl = url,
                state = connection.State.ToString()
            }, JsonOptions);
        }
        catch
        {
            Sessions.TryRemove(sessionId, out _);
            await connection.DisposeAsync();
            throw;
        }
    }

    public static string SubscribeAsync(string sessionId, string eventName, int argCount = 1)
    {
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session '{sessionId}' not found.");
        }

        var sanitizedArgCount = Math.Max(0, argCount);
        var parameterTypes = Enumerable.Repeat(typeof(object), sanitizedArgCount).ToArray();

        if (session.Subscriptions.TryRemove(eventName, out var existing))
        {
            existing.Dispose();
        }

        var subscription = session.Connection.On(
            eventName,
            parameterTypes,
            static (arguments, state) =>
            {
                var current = (SubscriptionState)state;
                current.Session.AddEvent(current.EventName, arguments);
                return Task.CompletedTask;
            },
            state: new SubscriptionState(session, eventName));

        session.Subscriptions[eventName] = subscription;
        session.AddLog("subscribed", $"Event '{eventName}' with {sanitizedArgCount} argument(s)");

        return JsonSerializer.Serialize(new
        {
            sessionId,
            eventName,
            argCount = sanitizedArgCount
        }, JsonOptions);
    }

    public static async Task<string> InvokeAsync(
        string sessionId,
        string methodName,
        string? argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session '{sessionId}' not found.");
        }

        var args = ParseArguments(argumentsJson);
        var result = await session.Connection.InvokeCoreAsync<object?>(methodName, args, cancellationToken);

        session.AddLog("invoke", $"Method '{methodName}' called with {args.Length} argument(s)");

        return JsonSerializer.Serialize(new
        {
            sessionId,
            methodName,
            result
        }, JsonOptions);
    }

    public static async Task<string> DisconnectAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!Sessions.TryRemove(sessionId, out var session))
        {
            return JsonSerializer.Serialize(new { sessionId, disconnected = false, reason = "not-found" }, JsonOptions);
        }

        foreach (var subscription in session.Subscriptions.Values)
        {
            subscription.Dispose();
        }

        try
        {
            await session.Connection.StopAsync(cancellationToken);
        }
        finally
        {
            await session.Connection.DisposeAsync();
        }

        session.AddLog("disconnected", "Connection disposed");

        return JsonSerializer.Serialize(new { sessionId, disconnected = true }, JsonOptions);
    }

    public static async Task<string> DrainEventsAsync(string sessionId, int durationSeconds = 0, CancellationToken cancellationToken = default)
    {
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session '{sessionId}' not found.");
        }

        var delay = Math.Max(0, durationSeconds);
        if (delay > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
        }

        var events = new List<SignalREventEntry>();
        while (session.Events.TryDequeue(out var entry))
        {
            events.Add(entry);
        }

        return JsonSerializer.Serialize(events, JsonOptions);
    }

    public static string GetLogs(string sessionId, int maxEntries = 100)
    {
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"Session '{sessionId}' not found.");
        }

        var take = Math.Clamp(maxEntries, 1, 1000);
        var logs = session.Logs.Reverse().Take(take).Reverse().ToList();

        return JsonSerializer.Serialize(logs, JsonOptions);
    }

    private static object?[] ParseArguments(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(argumentsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("argumentsJson must be a JSON array string.");
        }

        var args = new List<object?>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            args.Add(item.Clone());
        }

        return [.. args];
    }

    private sealed class SignalRSession(string id, string hubUrl, HubConnection connection)
    {
        public string Id { get; } = id;
        public string HubUrl { get; } = hubUrl;
        public HubConnection Connection { get; } = connection;
        public ConcurrentDictionary<string, IDisposable> Subscriptions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentQueue<SignalREventEntry> Events { get; } = new();
        public ConcurrentQueue<SignalRLogEntry> Logs { get; } = new();

        public void AddEvent(string eventName, IReadOnlyList<object?> arguments)
        {
            var serializedArgs = arguments.Select(a => a is null ? "null" : JsonSerializer.Serialize(a, JsonOptions)).ToList();
            Events.Enqueue(new SignalREventEntry(DateTimeOffset.UtcNow, eventName, serializedArgs));
        }

        public void AddLog(string type, string message)
        {
            Logs.Enqueue(new SignalRLogEntry(DateTimeOffset.UtcNow, type, message));
        }
    }

    private sealed record SubscriptionState(SignalRSession Session, string EventName);

    private sealed record SignalREventEntry(DateTimeOffset Timestamp, string EventName, IReadOnlyList<string> Arguments);
    private sealed record SignalRLogEntry(DateTimeOffset Timestamp, string Type, string Message);
}
