using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>
/// The server's half of server events (#291), over HTTP against a test server: the stream a page holds,
/// the requests that bind and release a topic, authorization by template, the publisher, the backplane
/// seam, the lifecycle handlers and the limits. One test per scenario of the <c>server-events</c> spec
/// that the server decides; the browser's half is the runtime's suite.
/// </summary>
public class ServerEventsTests
{
    private static readonly ServerTopic<Quote> Prices = new("prices");
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(200);

    private static ServerTopic<string> Room(string id) => new($"room:{id}");

    /// <summary>Waits for what the server does after a response has already gone out.</summary>
    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The server never got there.");
            await Task.Delay(20);
        }
    }

    private static Task<ServerEventsApp> AnonymousApp(Action<ServerEventsBuilder>? more = null) =>
        ServerEventsApp.StartAsync(events =>
        {
            events.Topic("prices", rule => rule.AllowAnonymous())
                .Topic("room:{roomId}", rule => rule.AllowAnonymous())
                .Configure(options => options.HeartbeatInterval = Quiet);
            more?.Invoke(events);
        });

    [Fact]
    public async Task AStream_IsAnEventStream_ThatHandsItsConnectionIdFirst()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();

        stream.ContentType.Should().Be("text/event-stream");
        stream.ConnectionId.Should().MatchRegex("^[A-Za-z0-9_-]{22}$", "a random token of 16 bytes, base64url");
    }

    [Fact]
    public async Task APublishedPayload_ReachesTheSubscribedPage_AsTheWireWritesIt()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "prices")).Should().BeNull();

        var quote = new Quote("EQ", 10.50m, 9_007_199_254_740_993);
        await app.PublishAsync(Prices, quote);

        var frame = await stream.NextEventAsync();
        frame.Event.Should().Be("message");
        var data = JsonDocument.Parse(frame.Data!).RootElement;
        data.GetProperty("topic").GetString().Should().Be("prices");
        data.GetProperty("payload").GetRawText().Should().Be(JsonSerializer.Serialize(quote, EqJson.Options),
            "the payload is written as a Server Action's result is, a long and a decimal as text");
    }

    [Fact]
    public async Task OneConnection_CarriesEveryTopicItHolds()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        foreach (var topic in new[] { "prices", "room:a", "room:b" })
            (await app.SubscribeAsync(stream.ConnectionId, topic)).Should().BeNull();

        await app.PublishAsync(Room("b"), "to b");
        await app.PublishAsync(Prices, new Quote("EQ", 1m, 1));
        await app.PublishAsync(Room("a"), "to a");

        var topics = new List<string?>();
        for (var i = 0; i < 3; i++)
            topics.Add(JsonDocument.Parse((await stream.NextEventAsync()).Data!).RootElement.GetProperty("topic").GetString());
        topics.Should().Equal("room:b", "prices", "room:a");
    }

    [Fact]
    public async Task ATopicNoTemplateMatches_IsRefused_AndReceivesNothing()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();

        (await app.SubscribeAsync(stream.ConnectionId, "orders:7")).Should().Be("unknown");
        await app.PublishAsync(new ServerTopic<string>("orders:7"), "secret");

        (await stream.NextAsync()).IsHeartbeat.Should().BeTrue("nothing was bound, so only the heartbeat arrives");
    }

    [Fact]
    public async Task APolicyTheRequestMeets_Binds_AndOneItDoesNot_IsRefused()
    {
        await using var app = await ServerEventsApp.StartAsync(
            events => events.Topic("room:{roomId}", rule => rule.RequireAuthorization("RoomMember"))
                .Configure(options => options.HeartbeatInterval = Quiet),
            services => services
                .AddAuthorization(options => options.AddPolicy("RoomMember", policy => policy.AddRequirements(new RoomMemberRequirement())))
                .AddSingleton<IAuthorizationHandler, RoomMemberHandler>());
        await using var stream = await app.OpenStreamAsync();

        (await app.SubscribeAsync(stream.ConnectionId, "room:a", request => request.Headers.Add("X-Room", "a"))).Should().BeNull();
        (await app.SubscribeAsync(stream.ConnectionId, "room:b", request => request.Headers.Add("X-Room", "a"))).Should().Be("forbidden");

        await app.PublishAsync(Room("b"), "not for this page");
        await app.PublishAsync(Room("a"), "for this page");
        JsonDocument.Parse((await stream.NextEventAsync()).Data!).RootElement.GetProperty("payload").GetString()
            .Should().Be("for this page");
    }

    [Fact]
    public async Task ADelegate_ReadsTheTemplatesValues_AndTheRequest()
    {
        ServerTopicContext? seen = null;
        string? path = null;
        await using var app = await ServerEventsApp.StartAsync(events => events.Topic(
            "room:{roomId}:participant:{participantId}",
            rule => rule.Authorize(topic =>
            {
                // Read while the request lasts: the server reuses an HttpContext once its request ends.
                seen = topic;
                path = topic.HttpContext.Request.Path.Value;
                return topic.Values["participantId"] == "7";
            })));
        await using var stream = await app.OpenStreamAsync();

        (await app.SubscribeAsync(stream.ConnectionId, "room:a:participant:7")).Should().BeNull();
        seen!.Values.Should().Contain("roomId", "a").And.Contain("participantId", "7");
        seen.ConnectionId.Should().Be(stream.ConnectionId);
        path.Should().EndWith("/subscribe");
        (await app.SubscribeAsync(stream.ConnectionId, "room:a:participant:8")).Should().Be("forbidden");
    }

    [Fact]
    public async Task TheTemplateWhoseLiteralsFixMore_Rules()
    {
        await using var app = await ServerEventsApp.StartAsync(events => events
            .Topic("room:{roomId}", rule => rule.AllowAnonymous())
            .Topic("room:{roomId}:participant:{participantId}", rule => rule.Authorize(_ => false)));
        await using var stream = await app.OpenStreamAsync();

        (await app.SubscribeAsync(stream.ConnectionId, "room:a:participant:7")).Should().Be("forbidden",
            "room:{roomId} also matches it, its parameter taking the rest, and the participant's template is the one the app meant");
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();
    }

    [Fact]
    public async Task RequiringAnAuthenticatedUser_RefusesAnAnonymousRequest()
    {
        await using var app = await ServerEventsApp.StartAsync(events => events.Topic("me", rule => rule.RequireAuthorization()));
        await using var stream = await app.OpenStreamAsync();

        (await app.SubscribeAsync(stream.ConnectionId, "me")).Should().Be("forbidden");
    }

    [Fact]
    public async Task AClientOnlyListens()
    {
        await using var app = await AnonymousApp();

        (await app.SubscribeAsync("not-a-connection", "prices")).Should().Be("HTTP 404");
        (await app.ReleaseAsync("not-a-connection", "prices")).Should().Be(HttpStatusCode.NotFound);
        using var publish = await app.Client.PostAsync("/_equantic/events", new StringContent("{}"));
        publish.IsSuccessStatusCode.Should().BeFalse("nothing a client sends publishes");
    }

    [Fact]
    public async Task AReleasedTopic_StopsReceiving()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();

        (await app.ReleaseAsync(stream.ConnectionId, "room:a")).Should().Be(HttpStatusCode.NoContent);
        await app.PublishAsync(Room("a"), "after the release");

        (await stream.NextAsync()).IsHeartbeat.Should().BeTrue();
    }

    [Fact]
    public async Task TheTopicLimit_IsReadFromConfiguration_AndRefusesTheNextSubscription()
    {
        await using var app = await ServerEventsApp.StartAsync(
            events => events.Topic("room:{roomId}", rule => rule.AllowAnonymous()),
            configuration: new Dictionary<string, string?> { ["EQuantic:ServerEvents:MaxTopicsPerConnection"] = "2" });
        await using var stream = await app.OpenStreamAsync();

        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();
        (await app.SubscribeAsync(stream.ConnectionId, "room:b")).Should().BeNull();
        (await app.SubscribeAsync(stream.ConnectionId, "room:c")).Should().Be("limitReached");
    }

    [Fact]
    public async Task APayloadPastTheLimit_ThrowsWhenPublished()
    {
        await using var app = await AnonymousApp(events => events.Configure(options => options.MaxPayloadBytes = 16));

        var publish = async () => await app.PublishAsync(Room("a"), new string('x', 64));

        (await publish.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*'room:a'*16 bytes*EQuantic:ServerEvents:MaxPayloadBytes*");
    }

    [Fact]
    public async Task AnEventsId_TravelsAsTheStreamsId_AndOneWithALineBreak_Throws()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();

        await app.PublishAsync(Room("a"), "seventh", new ServerEventPublishOptions { Id = "7" });
        (await stream.NextEventAsync()).Id.Should().Be("7");

        var broken = async () => await app.PublishAsync(Room("a"), "x", new ServerEventPublishOptions { Id = "7\n8" });
        await broken.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ALineSeparatorInsideAJsonString_StaysInTheString()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();

        // As a backplane may carry it: indented with CRLF, by a serializer that leaves U+2028 and NEL
        // unescaped, as JSON allows.
        const string payload = "{\r\n  \"text\": \"a\u2028b\u0085c\"\r\n}";
        await app.Services.GetRequiredService<IServerEventBackplane>()
            .PublishAsync(new ServerEventEnvelope("room:a", payload, null));

        var data = JsonDocument.Parse((await stream.NextEventAsync()).Data!).RootElement;
        data.GetProperty("payload").GetProperty("text").GetString().Should().Be("a\u2028b\u0085c");
    }

    [Fact]
    public async Task AFallbackPolicy_LeavesEachTopicToItsOwnRule()
    {
        await using var app = await ServerEventsApp.StartAsync(
            events => events.Topic("prices", rule => rule.AllowAnonymous()).Topic("me", rule => rule.RequireAuthorization()),
            services =>
            {
                services.AddAuthentication(NobodyAuthenticates.Name)
                    .AddScheme<AuthenticationSchemeOptions, NobodyAuthenticates>(NobodyAuthenticates.Name, null);
                services.AddAuthorization(options => options.FallbackPolicy =
                    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
            });
        await using var stream = await app.OpenStreamAsync();

        (await app.SubscribeAsync(stream.ConnectionId, "prices")).Should().BeNull("the app let anyone hear it");
        (await app.SubscribeAsync(stream.ConnectionId, "me")).Should().Be("forbidden");
    }

    [Fact]
    public async Task AStoppingApp_EndsItsStreams_SoTheirPagesConnectAgain()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();

        var stopping = app.StopAsync();

        var frames = 0;
        while (await stream.NextOrEndAsync(TimeSpan.FromSeconds(5)) is not null)
            frames++.Should().BeLessThan(10, "the heartbeat must not outlive the app");
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AnIdleConnection_SendsAHeartbeat()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();

        (await stream.NextAsync(TimeSpan.FromSeconds(5))).Comment.Should().Be("ping");
    }

    [Fact]
    public async Task TwoInstancesSharingABackplane_DeliverEachOthersEvents()
    {
        static Task<ServerEventsApp> Instance() => ServerEventsApp.StartAsync(events => events
            .Topic("room:{roomId}", rule => rule.AllowAnonymous())
            .UseBackplane<SharedBackplane>());
        await using var first = await Instance();
        await using var second = await Instance();
        await using var stream = await second.OpenStreamAsync();
        (await second.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();

        await first.PublishAsync(Room("a"), "published on the first instance");

        JsonDocument.Parse((await stream.NextEventAsync()).Data!).RootElement.GetProperty("payload").GetString()
            .Should().Be("published on the first instance");
    }

    [Fact]
    public async Task TheHandlers_HearTheLifecycle_WithTheRequestAtHand()
    {
        RecordingHandler.Heard.Clear();
        await using var app = await AnonymousApp(events => events.AddHandler<RecordingHandler>());
        var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();

        await stream.DisposeAsync();
        await Until(() => RecordingHandler.Heard.Contains("disconnected"));

        RecordingHandler.Heard.Should().Equal(
            "connected",
            $"subscribed room:a roomId=a path=/_equantic/events/{stream.ConnectionId}/subscribe",
            "released room:a",
            "disconnected");
    }

    [Fact]
    public async Task ATopicAuthorizedAfterItsStreamEnded_IsBoundToNothing()
    {
        RecordingHandler.Heard.Clear();
        var authorizing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decided = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = await ServerEventsApp.StartAsync(events => events
            .Topic("room:{roomId}", rule => rule.Authorize(async _ =>
            {
                authorizing.TrySetResult();
                return await decided.Task;
            }))
            .AddHandler<RecordingHandler>());
        var stream = await app.OpenStreamAsync();
        var subscribing = app.SubscribeAsync(stream.ConnectionId, "room:a");
        await authorizing.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await stream.DisposeAsync();
        await Until(() => RecordingHandler.Heard.Contains("disconnected"));
        decided.SetResult(true);

        (await subscribing).Should().Be("HTTP 404", "the stream it would have been bound to is gone");
        RecordingHandler.Heard.Should().Equal(new[] { "connected", "disconnected" },
            "a topic bound after the stream released everything would never be released, and a presence count would keep it");
    }

    [Fact]
    public async Task RequestsAtOnce_CannotPassTheTopicLimit()
    {
        const int requests = 6;
        var authorizing = 0;
        var allIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decided = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = await ServerEventsApp.StartAsync(
            events => events.Topic("room:{roomId}", rule => rule.Authorize(async _ =>
            {
                if (Interlocked.Increment(ref authorizing) == requests) allIn.TrySetResult();
                return await decided.Task;
            })),
            configuration: new Dictionary<string, string?> { ["EQuantic:ServerEvents:MaxTopicsPerConnection"] = "2" });
        await using var stream = await app.OpenStreamAsync();

        var subscribing = Enumerable.Range(0, requests).Select(i => app.SubscribeAsync(stream.ConnectionId, $"room:{i}")).ToList();
        await allIn.Task.WaitAsync(TimeSpan.FromSeconds(5));
        decided.SetResult(true);
        var answers = await Task.WhenAll(subscribing);

        answers.Count(answer => answer is null).Should().Be(2, "every request was under the limit when it began, and only two fit");
        answers.Where(answer => answer is not null).Should().AllBe("limitReached");
    }

    [Fact]
    public void ARuleThatSaysNothing_AndAConstrainedParameter_FailAtStartup()
    {
        var silent = () => new UIOptions().UseServerEvents(events => events.Topic("room:{roomId}", _ => { }));
        silent.Should().Throw<ArgumentException>().WithMessage("*AllowAnonymous()*");

        var constrained = () => new UIOptions().UseServerEvents(events =>
            events.Topic("room:{roomId:int}", rule => rule.AllowAnonymous()));
        constrained.Should().Throw<ArgumentException>().WithMessage("*constrains 'roomId'*");
    }

    [Fact]
    public void AFullQueue_ClosesTheConnection_InsteadOfGrowing()
    {
        var connection = new ServerEventConnection("id", capacity: 1);

        connection.Send("first");
        connection.Send("second");

        connection.Frames.TryRead(out var frame).Should().BeTrue();
        frame.Should().Be("first");
        connection.Frames.Completion.IsCompleted.Should().BeTrue("the second frame found the queue full and closed it");
    }

    /// <summary>
    /// The shell fetched SignalR's client from a CDN for every app that declared a server action, and
    /// nothing read it. Server events ship inside the runtime, so a page loads no script but the app's.
    /// </summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task APageOfAnAppWithServerActions_LoadsEveryScriptFromItsOwnOrigin(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options =>
        {
            // This assembly declares server actions (ServerActionAuthorizationServiceTests), which is
            // when the shell wrote the CDN's script.
            options.ScanAssembly(typeof(ServerEventsTests).Assembly);
            options.UseServerEvents(events => events.Topic("prices", rule => rule.AllowAnonymous()));
        });
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();

        var html = await app.GetTestClient().GetStringAsync("/untitled");

        var sources = Regex.Matches(html, @"<script\b[^>]*?\bsrc\s*=\s*[""']?([^""'\s>]+)", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value);
        var importMap = Regex.Match(html, @"<script type=""importmap"">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        var imports = JsonDocument.Parse(importMap).RootElement.GetProperty("imports").EnumerateObject()
            .Select(entry => entry.Value.GetString()!).ToList();

        imports.Should().NotBeEmpty("the runtime is imported through the import map");
        sources.Concat(imports).Should().OnlyContain(url => url.StartsWith('/') && !url.StartsWith("//"),
            "a script from another origin is a dependency the app never chose, fetched at run time");
    }

    [Fact]
    public async Task ServerRendering_HasAServerEventsCapability_ThatConnectsNothing()
    {
        await using var app = await AnonymousApp();

        var events = app.Services.GetRequiredService<IServerEvents>();
        events.Connection.Should().Be(ServerConnection.Disconnected);
        events.Subscribe(Prices, _ => throw new InvalidOperationException("nothing arrives here")).Dispose();
    }
}
