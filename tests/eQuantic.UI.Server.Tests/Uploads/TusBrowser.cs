using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>
/// A browser as the upload endpoint meets it: real tus requests (a creation with its length and
/// metadata, parts at an offset, HEAD, DELETE) and the Server Action call a page makes, carrying the
/// upload cookie the server gave it, and the user it is signed in as when it is.
/// </summary>
internal sealed class TusBrowser(HttpClient client, string? user = null, string? origin = null)
{
    private const string Endpoint = "/api/_equantic/uploads";

    /// <summary>The value of the cookie that names this browser's uploads, once the server gave one.</summary>
    public string? Cookie { get; set; }

    /// <summary>The <c>Set-Cookie</c> the server answered the last creation with, as written.</summary>
    public string? LastSetCookie { get; set; }

    public async Task<HttpResponseMessage> CreateAsync(long length, string? policy, string? fileName,
        string? fileType = "model/stl", bool deferLength = false, string? concatenation = null)
    {
        var request = Request(HttpMethod.Post, Endpoint);
        if (deferLength) request.Headers.Add("Upload-Defer-Length", "1");
        else request.Headers.Add("Upload-Length", length.ToString());
        if (concatenation is not null) request.Headers.Add("Upload-Concat", concatenation);
        var metadata = new[] { ("policy", policy), ("filename", fileName), ("filetype", fileType) }
            .Where(pair => pair.Item2 is not null)
            .Select(pair => $"{pair.Item1} {Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Item2!))}");
        request.Headers.Add("Upload-Metadata", string.Join(",", metadata));
        var response = await client.SendAsync(request);
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.FirstOrDefault(cookie => cookie.StartsWith("eq-uploads=", StringComparison.Ordinal)) is { } written)
        {
            LastSetCookie = written;
            Cookie = written["eq-uploads=".Length..written.IndexOf(';')];
        }

        return response;
    }

    public Task<HttpResponseMessage> PatchAsync(string location, long offset, byte[] part)
    {
        var request = Request(HttpMethod.Patch, location);
        request.Headers.Add("Upload-Offset", offset.ToString());
        request.Content = new ByteArrayContent(part);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        return client.SendAsync(request);
    }

    public Task<HttpResponseMessage> HeadAsync(string location) => client.SendAsync(Request(HttpMethod.Head, location));

    public Task<HttpResponseMessage> DeleteAsync(string location) => client.SendAsync(Request(HttpMethod.Delete, location));

    /// <summary>The offset the server holds for the upload, or null when it answered no offset.</summary>
    public async Task<long?> OffsetAsync(string location)
    {
        using var response = await HeadAsync(location);
        return response.Headers.TryGetValues("Upload-Offset", out var values) ? long.Parse(values.Single()) : null;
    }

    /// <summary>Creates an upload of <paramref name="content"/> and sends it in parts of
    /// <paramref name="partSize"/> bytes, the last of them only up to <paramref name="sendUpTo"/> when
    /// given; answers the upload's address.</summary>
    public async Task<string> UploadAsync(byte[] content, string fileName, string policy, int partSize, int? sendUpTo = null)
    {
        using var created = await CreateAsync(content.Length, policy, fileName);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var location = created.Headers.Location!.ToString();
        var end = sendUpTo ?? content.Length;
        for (var offset = 0; offset < end; offset += partSize)
        {
            using var patched = await PatchAsync(location, offset, content[offset..Math.Min(end, offset + partSize)]);
            patched.StatusCode.Should().Be(HttpStatusCode.NoContent, await patched.Content.ReadAsStringAsync());
        }

        return location;
    }

    /// <summary>Calls a Server Action with one argument, as the page's runtime does: the status, and the
    /// result or the error the server answered.</summary>
    public async Task<(HttpStatusCode Status, string Answer)> InvokeAsync(string action, object? argument)
    {
        var request = Request(HttpMethod.Post, "/api/_equantic/actions");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { actionId = $"{typeof(UploadReceivingActions).FullName}/{action}", arguments = new[] { argument } }),
            Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var answer = body.GetProperty("success").GetBoolean()
            ? body.GetProperty("result").GetString()!
            : body.GetProperty("error").GetString()!;
        return (response.StatusCode, answer);
    }

    /// <summary>The upload's id, the last segment of its address.</summary>
    public static string IdOf(string location) => location[(location.LastIndexOf('/') + 1)..];

    private HttpRequestMessage Request(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Tus-Resumable", "1.0.0");
        if (Cookie is not null) request.Headers.Add("Cookie", $"eq-uploads={Cookie}");
        if (user is not null) request.Headers.Add("X-User", user);
        if (origin is not null) request.Headers.Add("Origin", origin);
        return request;
    }
}
