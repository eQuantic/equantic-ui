using System.Text;
using System.Text.Json;

namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>
/// The page's side of the stream, read as the browser's <c>EventSource</c> reads it: frames separated
/// by a blank line, <c>event:</c>, <c>data:</c> and <c>id:</c> fields, and a comment line for the
/// heartbeat. Every read has a deadline, so a frame that never comes fails the test instead of
/// hanging it.
/// </summary>
internal sealed class ServerEventStream : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly StreamReader _reader;

    private ServerEventStream(HttpResponseMessage response, Stream body)
    {
        _response = response;
        _reader = new StreamReader(body, Encoding.UTF8);
    }

    public string ConnectionId { get; private set; } = "";

    public string? ContentType => _response.Content.Headers.ContentType?.MediaType;

    public static async Task<ServerEventStream> OpenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/_equantic/events", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var stream = new ServerEventStream(response, await response.Content.ReadAsStreamAsync());
        var first = await stream.NextAsync();
        if (first.Event != "connection") throw new InvalidOperationException($"The stream began with '{first.Event}'.");
        stream.ConnectionId = JsonDocument.Parse(first.Data!).RootElement.GetProperty("id").GetString()!;
        return stream;
    }

    /// <summary>The next frame, or null when the server ended the stream.</summary>
    public async Task<ServerEventFrame?> NextOrEndAsync(TimeSpan? deadline = null)
    {
        using var timeout = new CancellationTokenSource(deadline ?? TimeSpan.FromSeconds(10));
        string? @event = null, id = null, comment = null;
        var data = new List<string>();
        while (true)
        {
            var line = await _reader.ReadLineAsync(timeout.Token);
            if (line is null) return null;
            if (line.Length == 0)
            {
                if (@event is null && data.Count == 0 && id is null && comment is null) continue;
                return new ServerEventFrame(@event, data.Count == 0 ? null : string.Join("\n", data), id, comment);
            }
            if (line.StartsWith(':')) comment = line[1..].Trim();
            else if (line.StartsWith("event: ", StringComparison.Ordinal)) @event = line["event: ".Length..];
            else if (line.StartsWith("data: ", StringComparison.Ordinal)) data.Add(line["data: ".Length..]);
            else if (line.StartsWith("id: ", StringComparison.Ordinal)) id = line["id: ".Length..];
        }
    }

    public async Task<ServerEventFrame> NextAsync(TimeSpan? deadline = null) =>
        await NextOrEndAsync(deadline) ?? throw new InvalidOperationException("The server ended the stream.");

    /// <summary>The next frame that is not a heartbeat.</summary>
    public async Task<ServerEventFrame> NextEventAsync(TimeSpan? deadline = null)
    {
        while (true)
        {
            var frame = await NextAsync(deadline);
            if (!frame.IsHeartbeat) return frame;
        }
    }

    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        _response.Dispose();
        return ValueTask.CompletedTask;
    }
}
