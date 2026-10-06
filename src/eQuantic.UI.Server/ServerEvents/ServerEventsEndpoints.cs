using System.Text.Json;
using System.Threading.Channels;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// The three endpoints of the pages' connections: the stream a page holds open, and the two requests
/// that bind and release a topic on it. Nothing here publishes: a client only listens.
/// </summary>
internal static class ServerEventsEndpoints
{
    /// <summary>Where the stream is served; the runtime's client reads the same path.</summary>
    public const string Path = "/_equantic/events";

    /// <summary>The longest topic name a request may carry.</summary>
    private const int MaxTopicLength = 512;

    /// <summary>
    /// The largest body a bind or release request may send: a topic of <see cref="MaxTopicLength"/>
    /// characters, each escaped as JSON may escape it, with room to spare. The endpoints are open to
    /// anyone, and without a cap a request made the server parse whatever Kestrel's own limit lets
    /// through to read one short string.
    /// </summary>
    private const int MaxBodyBytes = 8 * 1024;

    /// <summary>The refusal's body is the client's protocol, so the app's own JSON settings never rename it.</summary>
    private static readonly JsonSerializerOptions Protocol = new();

    /// <summary>
    /// Maps the three endpoints, each open to an anonymous request: what a page may hear is decided per
    /// topic, by the rules the app configured, and a stream carries nothing until a topic is bound to
    /// it. An app's fallback authorization policy would otherwise refuse the stream itself, and with it
    /// every topic the app allowed anyone to hear.
    /// </summary>
    public static IEndpointRouteBuilder MapServerEvents(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, StreamAsync).AllowAnonymous();
        endpoints.MapPost(Path + "/{connection}/subscribe", SubscribeAsync).AllowAnonymous();
        endpoints.MapPost(Path + "/{connection}/release", ReleaseAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task StreamAsync(HttpContext http)
    {
        var options = http.RequestServices.GetRequiredService<IOptions<ServerEventsOptions>>().Value;
        var connections = http.RequestServices.GetRequiredService<ServerEventConnections>();
        // The stream ends with its request or with the app. A graceful shutdown waits for every
        // request in flight, and a stream left open held the instance until the host's timeout
        // instead of sending its page to connect again, to this instance once it is back or to
        // another one.
        var stopping = http.RequestServices.GetService<IHostApplicationLifetime>()?.ApplicationStopping
                       ?? CancellationToken.None;

        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        // A reverse proxy that buffers the response would hold every event back until it closes.
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var connection = connections.Open(options.MaxQueuedEventsPerConnection);
        // ...and with its own queue's overflow, which a page that stopped reading causes while the
        // stream is blocked writing to it.
        using var ending = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, stopping, connection.Overflowed);
        var aborted = ending.Token;
        try
        {
            await Notify(http, handler => handler.OnConnectedAsync(new ServerEventConnectionContext(connection.Id, http)));
            await WriteAsync(http, ServerEventFrames.Connected(connection.Id), aborted);

            while (!aborted.IsCancellationRequested)
            {
                string frame;
                using (var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(aborted))
                {
                    heartbeat.CancelAfter(options.HeartbeatInterval);
                    try
                    {
                        frame = await connection.Frames.ReadAsync(heartbeat.Token);
                    }
                    catch (OperationCanceledException) when (!aborted.IsCancellationRequested)
                    {
                        frame = ServerEventFrames.Heartbeat;
                    }
                    catch (ChannelClosedException)
                    {
                        // Closed because the page stopped reading: ending the response makes it connect again.
                        break;
                    }
                }

                await WriteAsync(http, frame, aborted);
            }
        }
        catch (OperationCanceledException) when (aborted.IsCancellationRequested)
        {
            // The page went away, or the app is stopping.
        }
        finally
        {
            // After any bind or release still telling its handlers: they hear this one's leave last.
            await connection.Transitions.WaitAsync();
            try
            {
                foreach (var topic in connections.Close(connection, http))
                    await Notify(http, handler => handler.OnReleasedAsync(topic));
                await Notify(http, handler => handler.OnDisconnectedAsync(new ServerEventConnectionContext(connection.Id, http)));
            }
            finally
            {
                connection.Transitions.Release();
            }
        }
    }

    private static async Task SubscribeAsync(HttpContext http)
    {
        if (!AcceptsBody(http)) return;
        var connections = http.RequestServices.GetRequiredService<ServerEventConnections>();
        if (!connections.TryGet(http.Request.RouteValues["connection"] as string ?? "", out var connection))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var topic = await ReadTopicAsync(http);
        if (topic is null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (connection!.Holds(topic))
        {
            http.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        // Read again when the topic is bound: this one only spares the authorization a request that
        // is already over the limit.
        var limit = http.RequestServices.GetRequiredService<IOptions<ServerEventsOptions>>().Value.MaxTopicsPerConnection;
        if (connection.TopicCount >= limit)
        {
            await RefuseAsync(http, ServerTopicRefusalReason.LimitReached);
            return;
        }

        var authorizer = http.RequestServices.GetRequiredService<ServerTopicAuthorizer>();
        var answer = await authorizer.AuthorizeAsync(http, connection.Id, topic);
        if (answer.Context is null)
        {
            await RefuseAsync(http, answer.Refusal ?? ServerTopicRefusalReason.Forbidden);
            return;
        }

        // The bind and its subscribed are one transition: a close that comes while the handlers hear it
        // waits for them, and they hear the leave after the join.
        ServerTopicBinding binding;
        await connection.Transitions.WaitAsync(http.RequestAborted);
        try
        {
            binding = connections.Bind(connection, answer.Context, limit);
            if (binding == ServerTopicBinding.Bound)
                await Notify(http, handler => handler.OnSubscribedAsync(answer.Context));
        }
        finally
        {
            connection.Transitions.Release();
        }

        switch (binding)
        {
            case ServerTopicBinding.LimitReached:
                await RefuseAsync(http, ServerTopicRefusalReason.LimitReached);
                return;
            case ServerTopicBinding.ConnectionClosed:
                // The stream ended while the topic was being authorized: the page's next connection
                // binds it again.
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
        }

        http.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static async Task ReleaseAsync(HttpContext http)
    {
        if (!AcceptsBody(http)) return;
        var connections = http.RequestServices.GetRequiredService<ServerEventConnections>();
        if (!connections.TryGet(http.Request.RouteValues["connection"] as string ?? "", out var connection))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var topic = await ReadTopicAsync(http);
        if (topic is null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await connection!.Transitions.WaitAsync(http.RequestAborted);
        try
        {
            if (connections.Release(connection, topic, http) is { } released)
                await Notify(http, handler => handler.OnReleasedAsync(released));
        }
        finally
        {
            connection.Transitions.Release();
        }
        http.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    /// <summary>
    /// Whether a bind or release request may be read: a JSON body, within <see cref="MaxBodyBytes"/>.
    /// JSON is what makes the request the page's own. A topic is authorized as the request that binds
    /// it, cookies included, and bound to whichever connection its path names, so a hostile page that
    /// could send one would bind its visitor's topics to a stream it opened itself. A body of any
    /// other type is a request a form or a <c>no-cors</c> fetch can send from another site; a JSON one
    /// from another origin needs the browser's preflight, which an app without a CORS policy for that
    /// origin refuses before the request leaves.
    /// </summary>
    private static bool AcceptsBody(HttpContext http)
    {
        if (!http.Request.HasJsonContentType())
        {
            http.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return false;
        }

        if (http.Request.ContentLength > MaxBodyBytes)
        {
            http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return false;
        }

        // A chunked body states no length: Kestrel enforces the cap while it reads.
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = MaxBodyBytes;
        return true;
    }

    /// <summary>The topic a bind or release request names, or null when the body does not name one.</summary>
    private static async Task<string?> ReadTopicAsync(HttpContext http)
    {
        try
        {
            using var body = await JsonDocument.ParseAsync(http.Request.Body, new JsonDocumentOptions { MaxDepth = 4 },
                http.RequestAborted);
            return body.RootElement.ValueKind == JsonValueKind.Object
                   && body.RootElement.TryGetProperty("topic", out var topic)
                   && topic.ValueKind == JsonValueKind.String
                   && topic.GetString() is { Length: > 0 and <= MaxTopicLength } name
                ? name
                : null;
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return null;
        }
    }

    private static async Task RefuseAsync(HttpContext http, ServerTopicRefusalReason reason)
    {
        http.Response.StatusCode = StatusCodes.Status403Forbidden;
        await http.Response.WriteAsJsonAsync(new { reason = JsonNamingPolicy.CamelCase.ConvertName(reason.ToString()) },
            Protocol);
    }

    private static async Task WriteAsync(HttpContext http, string frame, CancellationToken aborted)
    {
        await http.Response.WriteAsync(frame, aborted);
        await http.Response.Body.FlushAsync(aborted);
    }

    /// <summary>Tells every registered handler; one that throws is logged and the rest still hear it.</summary>
    private static async Task Notify(HttpContext http, Func<IServerEventHandler, ValueTask> notify)
    {
        foreach (var handler in http.RequestServices.GetServices<IServerEventHandler>())
        {
            try
            {
                await notify(handler);
            }
            catch (Exception exception)
            {
                http.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger("eQuantic.UI.Server.ServerEvents")
                    .LogError(exception, "The server events handler {Handler} threw.", handler.GetType().FullName);
            }
        }
    }
}
