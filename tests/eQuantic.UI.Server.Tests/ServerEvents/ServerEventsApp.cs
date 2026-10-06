using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>An app with server events configured, on a test server: the real endpoints, the real
/// publisher and the real authorization, reached over HTTP as a page reaches them.</summary>
internal sealed class ServerEventsApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ServerEventsApp(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<ServerEventsApp> StartAsync(
        Action<ServerEventsBuilder> events, Action<IServiceCollection>? services = null,
        IReadOnlyDictionary<string, string?>? configuration = null, Action<UIOptions>? ui = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Directory.CreateTempSubdirectory("eq-server-events-").FullName,
        });
        builder.WebHost.UseTestServer();
        if (configuration is not null) builder.Configuration.AddInMemoryCollection(configuration);
        services?.Invoke(builder.Services);
        builder.Services.AddUI(options =>
        {
            ui?.Invoke(options);
            options.UseServerEvents(events);
        });
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        return new ServerEventsApp(app);
    }

    public Task<ServerEventStream> OpenStreamAsync() => ServerEventStream.OpenAsync(Client);

    /// <summary>Binds a topic: null when the server bound it, or the reason it gave.</summary>
    public async Task<string?> SubscribeAsync(string connection, string topic, Action<HttpRequestMessage>? request = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/_equantic/events/{connection}/subscribe")
        {
            Content = JsonContent.Create(new { topic }),
        };
        request?.Invoke(message);
        using var response = await Client.SendAsync(message);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        if (response.StatusCode != HttpStatusCode.Forbidden) return $"HTTP {(int)response.StatusCode}";
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("reason").GetString();
    }

    public async Task<HttpStatusCode> ReleaseAsync(string connection, string topic)
    {
        using var response = await Client.PostAsJsonAsync($"/_equantic/events/{connection}/release", new { topic });
        return response.StatusCode;
    }

    public ValueTask PublishAsync<T>(ServerTopic<T> topic, T payload, ServerEventPublishOptions? options = null) =>
        Services.GetRequiredService<IServerEventPublisher>().PublishAsync(topic, payload, options);

    public Task StopAsync() => _app.StopAsync();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
