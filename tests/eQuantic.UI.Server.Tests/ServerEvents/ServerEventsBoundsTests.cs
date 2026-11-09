using System.Net;
using System.Net.Http.Json;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>
/// #1292: where a request to the streams may come from, how many streams an instance holds and for how
/// long, what the app may attach to the endpoints, and what a frame from the backplane may carry.
/// </summary>
public class ServerEventsBoundsTests
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(200);

    private static Task<ServerEventsApp> AnonymousApp(Action<ServerEventsOptions>? options = null,
        Action<ServerEventsBuilder>? more = null, IReadOnlyDictionary<string, string?>? configuration = null) =>
        ServerEventsApp.StartAsync(events =>
        {
            events.Topic("room:{roomId}", rule => rule.AllowAnonymous())
                .Configure(o =>
                {
                    o.HeartbeatInterval = Quiet;
                    options?.Invoke(o);
                });
            more?.Invoke(events);
        }, configuration: configuration);

    private static async Task<HttpStatusCode> PostFrom(ServerEventsApp app, string path, string? origin)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { topic = "room:a" }) };
        if (origin is not null) message.Headers.Add("Origin", origin);
        using var response = await app.Client.SendAsync(message);
        return response.StatusCode;
    }

    [Fact]
    public async Task ABindOrAReleaseFromAnotherSite_IsRefused()
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        var subscribe = $"/_equantic/events/{stream.ConnectionId}/subscribe";
        var release = $"/_equantic/events/{stream.ConnectionId}/release";

        (await PostFrom(app, subscribe, "https://elsewhere.example")).Should().Be(HttpStatusCode.Forbidden);
        (await PostFrom(app, release, "https://elsewhere.example")).Should().Be(HttpStatusCode.Forbidden);
        (await PostFrom(app, subscribe, "http://localhost")).Should().Be(HttpStatusCode.NoContent,
            "the app's own host binds as before");
    }

    [Fact]
    public async Task ABindFromAnOriginTheAppAllows_Binds()
    {
        await using var app = await AnonymousApp(configuration: new Dictionary<string, string?>
        {
            [$"{ServerActionsOptions.SectionName}:AllowedOrigins:0"] = "https://partner.example",
        });
        await using var stream = await app.OpenStreamAsync();

        (await PostFrom(app, $"/_equantic/events/{stream.ConnectionId}/subscribe", "https://partner.example"))
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AStreamFromAnotherSite_IsNotOpened()
    {
        await using var app = await AnonymousApp();
        using var message = new HttpRequestMessage(HttpMethod.Get, "/_equantic/events");
        message.Headers.Add("Origin", "https://elsewhere.example");

        using var response = await app.Client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AStreamPastTheInstancesCap_IsToldToComeBack()
    {
        await using var app = await AnonymousApp(o => o.MaxConnections = 1);
        await using var first = await app.OpenStreamAsync();

        using var second = await app.Client.GetAsync("/_equantic/events", HttpCompletionOption.ResponseHeadersRead);

        second.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        second.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task AStreamPastItsLifetime_Ends_SoItsTopicsAreBoundAndAuthorizedAgain()
    {
        await using var app = await AnonymousApp(o => o.MaxConnectionLifetime = TimeSpan.FromMilliseconds(300));
        await using var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        ServerEventFrame? frame;
        do frame = await stream.NextOrEndAsync(TimeSpan.FromSeconds(5));
        while (frame is not null && DateTime.UtcNow < deadline);

        frame.Should().BeNull("the server ended the stream when its lifetime ran out");
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().Be("HTTP 404",
            "the old connection holds nothing any more");
    }

    [Theory]
    [InlineData("a\nb", "{}")]
    [InlineData("a\rb", "{}")]
    [InlineData("a\0b", "{}")]
    [InlineData(null, "{} {}")]
    [InlineData(null, "{")]
    [InlineData(null, "")]
    public async Task AFrameTheBackplaneCarriesThatAFrameCannotHold_ReachesNoPage(string? id, string payload)
    {
        await using var app = await AnonymousApp();
        await using var stream = await app.OpenStreamAsync();
        (await app.SubscribeAsync(stream.ConnectionId, "room:a")).Should().BeNull();
        var backplane = app.Services.GetRequiredService<IServerEventBackplane>();

        await backplane.PublishAsync(new ServerEventEnvelope("room:a", payload, id));
        await backplane.PublishAsync(new ServerEventEnvelope("room:a", "\"after\"", "next"));

        var frame = await stream.NextEventAsync();
        frame.Id.Should().Be("next", "the frame before it was written to no page");
    }

    [Fact]
    public async Task WhatTheAppAttachesToTheEndpoints_ReachesAllThree()
    {
        var marker = new object();
        await using var app = await AnonymousApp(more: events => events.ConfigureEndpoints(endpoints =>
            endpoints.Add(builder => builder.Metadata.Add(marker))));

        var events = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/_equantic/events", StringComparison.Ordinal) == true)
            .ToList();

        events.Should().HaveCount(3);
        events.Should().OnlyContain(endpoint => endpoint.Metadata.Contains(marker));
    }
}
