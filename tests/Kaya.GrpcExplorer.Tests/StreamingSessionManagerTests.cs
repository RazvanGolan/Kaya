using FluentAssertions;
using Google.Protobuf.Reflection;
using Kaya.GrpcExplorer.Services;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

public class StreamingSessionManagerTests
{
    private readonly StreamingSessionManager _manager = new();

    [Fact]
    public void Add_ShouldStoreSession_SoGetReturnsIt()
    {
        var session = new StreamingSession { MethodDescriptor = null! };

        _manager.Add(session);

        _manager.Get(session.Id).Should().BeSameAs(session);
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenSessionIdDoesNotExist()
    {
        var result = _manager.Get("does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveSession_SoGetReturnsNullAfterwards()
    {
        var session = new StreamingSession { MethodDescriptor = null! };
        _manager.Add(session);

        await _manager.RemoveAsync(session.Id);

        _manager.Get(session.Id).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldNotThrow_WhenSessionIdDoesNotExist()
    {
        var act = async () => await _manager.RemoveAsync("nonexistent-session");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Add_ShouldAllowMultipleSessions()
    {
        var s1 = new StreamingSession { MethodDescriptor = null! };
        var s2 = new StreamingSession { MethodDescriptor = null! };

        _manager.Add(s1);
        _manager.Add(s2);

        _manager.Get(s1.Id).Should().BeSameAs(s1);
        _manager.Get(s2.Id).Should().BeSameAs(s2);
    }

    [Fact]
    public async Task RemoveAsync_ShouldOnlyRemoveTargetSession()
    {
        var s1 = new StreamingSession { MethodDescriptor = null! };
        var s2 = new StreamingSession { MethodDescriptor = null! };
        _manager.Add(s1);
        _manager.Add(s2);

        await _manager.RemoveAsync(s1.Id);

        _manager.Get(s1.Id).Should().BeNull();
        _manager.Get(s2.Id).Should().BeSameAs(s2);

        // cleanup
        await _manager.RemoveAsync(s2.Id);
    }
}

public class StreamingSessionTests
{
    [Fact]
    public void Id_ShouldBe32CharacterHexString()
    {
        var session = new StreamingSession { MethodDescriptor = null! };

        session.Id.Should().HaveLength(32);
        session.Id.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void Id_ShouldBeUniqueAcrossInstances()
    {
        var s1 = new StreamingSession { MethodDescriptor = null! };
        var s2 = new StreamingSession { MethodDescriptor = null! };

        s1.Id.Should().NotBe(s2.Id);
    }

    [Fact]
    public void EventWriter_ShouldWriteEventsReadableViaEventsReader()
    {
        var session = new StreamingSession { MethodDescriptor = null! };
        var evt = new SseEvent(SseEventType.Message, "payload");

        var written = session.EventWriter.TryWrite(evt);

        written.Should().BeTrue();
        session.Events.TryRead(out var read).Should().BeTrue();
        read!.Payload.Should().Be("payload");
        read.Type.Should().Be(SseEventType.Message);
    }

    [Fact]
    public void RequestWriter_ShouldBeNullByDefault()
    {
        var session = new StreamingSession { MethodDescriptor = null! };

        session.RequestWriter.Should().BeNull();
    }

    [Fact]
    public void Cts_ShouldNotBeCancelledInitially()
    {
        var session = new StreamingSession { MethodDescriptor = null! };

        session.Cts.IsCancellationRequested.Should().BeFalse();
        // cleanup
        session.Cts.Cancel();
        session.Cts.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteEventWriter_SoTryWriteReturnsFalse()
    {
        var session = new StreamingSession { MethodDescriptor = null! };

        await session.DisposeAsync();

        // After disposal the channel writer is completed; writes should be rejected
        var result = session.EventWriter.TryWrite(new SseEvent(SseEventType.Message, "after dispose"));
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_ShouldAwaitResponseReaderTask_WhenSet()
    {
        var tcs = new TaskCompletionSource();
        var session = new StreamingSession
        {
            MethodDescriptor = null!,
            ResponseReaderTask = tcs.Task
        };
        tcs.SetResult(); // complete immediately

        var act = async () => await session.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotThrow_WhenResponseReaderThrows()
    {
        var tcs = new TaskCompletionSource();
        var session = new StreamingSession
        {
            MethodDescriptor = null!,
            ResponseReaderTask = tcs.Task
        };
        tcs.SetException(new InvalidOperationException("simulated reader error"));

        var act = async () => await session.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
