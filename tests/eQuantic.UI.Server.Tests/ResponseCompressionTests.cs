using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// What an app serves is compressed, and the developer writes nothing for it (#1002). <c>AddUI</c> registered
/// the providers and nothing applied them: measured on the blank template published in Release,
/// <c>/_equantic/runtime.js</c> came back at 939,966 bytes with <c>Accept-Encoding: br</c>.
/// </summary>
public class ResponseCompressionTests
{
    private static async Task<(WebApplication App, HttpClient Client, string Module)> StartAppAsync()
    {
        var webRoot = Directory.CreateTempSubdirectory("eq-compression-").FullName;
        var modules = Directory.CreateDirectory(Path.Combine(webRoot, "_equantic")).FullName;
        var module = string.Concat(Enumerable.Repeat("export const page = 'home';\n", 400));
        File.WriteAllText(Path.Combine(modules, "HomePage.js"), module);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI();
        var app = builder.Build();
        app.UseStaticFiles();
        app.MapUI();
        await app.StartAsync();
        return (app, app.GetTestClient(), module);
    }

    private static async Task<HttpResponseMessage> Get(HttpClient client, string path, string? encoding)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (encoding is not null) request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
        return await client.SendAsync(request);
    }

    private static async Task<byte[]> Decoded(HttpResponseMessage response, string encoding)
    {
        using var encoded = await response.Content.ReadAsStreamAsync();
        using Stream reader = encoding == "br"
            ? new BrotliStream(encoded, CompressionMode.Decompress)
            : new GZipStream(encoded, CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        await reader.CopyToAsync(decoded);
        return decoded.ToArray();
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task TheRuntime_IsCompressed_AndDecompressesToTheSameBytes(string encoding)
    {
        var (app, client, _) = await StartAppAsync();
        await using var _1 = app;

        var plain = await (await Get(client, "/_equantic/runtime.js", null)).Content.ReadAsByteArrayAsync();
        var response = await Get(client, "/_equantic/runtime.js", encoding);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.Should().Equal(encoding);
        var decoded = await Decoded(response, encoding);
        decoded.Should().Equal(plain);
        plain.Length.Should().BeGreaterThan(1000, "the served runtime is no stub");
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task AModule_IsCompressed(string encoding)
    {
        var (app, client, module) = await StartAppAsync();
        await using var _1 = app;

        var response = await Get(client, "/_equantic/HomePage.js", encoding);

        response.Content.Headers.ContentEncoding.Should().Equal(encoding);
        System.Text.Encoding.UTF8.GetString(await Decoded(response, encoding)).Should().Be(module);
    }

    [Fact]
    public async Task ABrowserThatAsksForNoEncoding_GetsThePlainFile()
    {
        var (app, client, module) = await StartAppAsync();
        await using var _1 = app;

        var response = await Get(client, "/_equantic/HomePage.js", null);

        response.Content.Headers.ContentEncoding.Should().BeEmpty();
        (await response.Content.ReadAsStringAsync()).Should().Be(module);
    }
}
