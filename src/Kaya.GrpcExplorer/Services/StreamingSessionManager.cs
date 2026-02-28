using System.Collections.Concurrent;
using System.Threading.Channels;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;

namespace Kaya.GrpcExplorer.Services;

/// <summary>
/// Discriminated event type pushed over SSE
/// </summary>
public enum SseEventType { Message, Complete, Error }

public record SseEvent(SseEventType Type, string Payload);

/// <summary>
/// Holds state for one active interactive streaming session
/// </summary>
public sealed class StreamingSession : IAsyncDisposable
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public MethodDescriptor MethodDescriptor { get; init; } = null!;

    // Unbounded channel — responses are queued here and drained by the SSE handler
    private readonly Channel<SseEvent> _channel = Channel.CreateUnbounded<SseEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ChannelReader<SseEvent> Events => _channel.Reader;
    public ChannelWriter<SseEvent> EventWriter => _channel.Writer;

    // Request stream writer — null for server-streaming (no client messages)
    public IClientStreamWriter<IMessage>? RequestWriter { get; init; }

    public CancellationTokenSource Cts { get; } = new();

    // Background task reading server responses (duplex / server streaming)
    public Task? ResponseReaderTask { get; set; }

    public async ValueTask DisposeAsync()
    {
        await Cts.CancelAsync();
        _channel.Writer.TryComplete();
        if (ResponseReaderTask is not null)
        {
            try { await ResponseReaderTask; }
            catch { /* already cancelled */ }
        }
        Cts.Dispose();
    }
}

/// <summary>
/// Manages active interactive gRPC streaming sessions
/// </summary>
public interface IStreamingSessionManager
{
    void Add(StreamingSession session);
    StreamingSession? Get(string sessionId);
    Task RemoveAsync(string sessionId);
}

public sealed class StreamingSessionManager : IStreamingSessionManager
{
    private readonly ConcurrentDictionary<string, StreamingSession> _sessions = new();

    public void Add(StreamingSession session) =>
        _sessions[session.Id] = session;

    public StreamingSession? Get(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public async Task RemoveAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            await session.DisposeAsync();
        }
    }
}
