using System.Collections.Concurrent;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>
/// A presence tracker whose join is slow, as one that writes to a store is: it holds every topic's
/// subscribed until the test lets it through, and writes down what it hears in the order it hears it.
/// </summary>
internal sealed class SlowJoinHandler : IServerEventHandler
{
    public static readonly ConcurrentQueue<string> Heard = new();

    /// <summary>Set when a subscribed has started and is waiting.</summary>
    public static TaskCompletionSource Joining { get; private set; } = New();

    /// <summary>What lets a waiting subscribed through.</summary>
    public static TaskCompletionSource Joined { get; private set; } = New();

    public static void Reset()
    {
        Heard.Clear();
        Joining = New();
        Joined = New();
    }

    public ValueTask OnConnectedAsync(ServerEventConnectionContext connection)
    {
        Heard.Enqueue("connected");
        return ValueTask.CompletedTask;
    }

    public async ValueTask OnSubscribedAsync(ServerTopicContext topic)
    {
        Joining.TrySetResult();
        await Joined.Task;
        Heard.Enqueue($"subscribed {topic.Topic}");
    }

    public ValueTask OnReleasedAsync(ServerTopicContext topic)
    {
        Heard.Enqueue($"released {topic.Topic}");
        return ValueTask.CompletedTask;
    }

    public ValueTask OnDisconnectedAsync(ServerEventConnectionContext connection)
    {
        Heard.Enqueue("disconnected");
        return ValueTask.CompletedTask;
    }

    private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
