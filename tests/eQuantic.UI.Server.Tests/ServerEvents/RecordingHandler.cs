using System.Collections.Concurrent;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>Writes down what it hears, in order, the way an app's presence tracker would read it.</summary>
internal sealed class RecordingHandler : IServerEventHandler
{
    public static readonly ConcurrentQueue<string> Heard = new();

    public ValueTask OnConnectedAsync(ServerEventConnectionContext connection)
    {
        Heard.Enqueue("connected");
        return ValueTask.CompletedTask;
    }

    public ValueTask OnSubscribedAsync(ServerTopicContext topic)
    {
        Heard.Enqueue($"subscribed {topic.Topic} roomId={topic.Values["roomId"]} path={topic.HttpContext.Request.Path}");
        return ValueTask.CompletedTask;
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
}
