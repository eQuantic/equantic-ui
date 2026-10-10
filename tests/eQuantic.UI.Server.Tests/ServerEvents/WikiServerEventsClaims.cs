using System.Globalization;
using System.Security.Claims;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>
/// The server's half of the wiki's ServerEvents page, as the page writes it: the publisher, the
/// rules and the presence handler compile, and the configuration passes the checks the builder makes
/// at startup. Nothing else compiles the wiki, so this is what keeps the page from drifting from the
/// surface it documents (#291).
/// </summary>
public class WikiServerEventsClaims
{
    private sealed record RoomMessage(string Author, string Text, long Sequence);

    private static class Topics
    {
        public static ServerTopic<RoomMessage> Room(string id) => new($"room:{id}");
    }

    private sealed class RoomService(IServerEventPublisher events)
    {
        public ValueTask PostAsync(string room, RoomMessage message, CancellationToken cancellationToken) =>
            events.PublishAsync(Topics.Room(room), message,
                new ServerEventPublishOptions { Id = message.Sequence.ToString(CultureInfo.InvariantCulture) },
                cancellationToken);
    }

    private interface IRoomRegistry
    {
        ValueTask JoinAsync(string room, string connection);

        ValueTask LeaveAsync(string room, string connection);
    }

    private sealed class RoomPresence(IRoomRegistry rooms) : IServerEventHandler
    {
        public ValueTask OnSubscribedAsync(ServerTopicContext topic) =>
            rooms.JoinAsync(topic.Values["roomId"]!, topic.ConnectionId);

        public ValueTask OnReleasedAsync(ServerTopicContext topic) =>
            rooms.LeaveAsync(topic.Values["roomId"]!, topic.ConnectionId);
    }

    [Fact]
    public void TheRules_AndTheHandler_PassTheChecksMadeAtStartup()
    {
        var configure = () => new UIOptions()
            .UseServerEvents(events => events
                .Topic("prices", rule => rule.AllowAnonymous())
                .Topic("room:{roomId}", rule => rule.RequireAuthorization("RoomMember"))
                .Topic("user:{userId}", rule => rule.Authorize(topic =>
                    topic.Values["userId"] == topic.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)))
                .AddHandler<RoomPresence>());

        configure.Should().NotThrow();
        typeof(RoomService).Should().NotBeNull("the publisher compiles as the page writes it");
    }
}
