using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>
/// The wiki's Uploads page as an app writes it in <c>Program.cs</c> (#1215), compiled and run here,
/// where the server is referenced: the line that accepts the policy maps the endpoint, and the page's
/// server render is handed an <see cref="IUploads"/> that says it cannot send, with an upload that has
/// already failed rather than a throw that would fail the render.
/// </summary>
public sealed class UploadsWikiTests
{
    private static class Uploads
    {
        public static readonly UploadPolicy Models = new("models",
            filters: [new FileFilter("3D models", "stl", "obj", "3mf")],
            maxSize: 250 * 1024 * 1024);
    }

    private sealed class ChosenFile() : PickedFile("bracket.stl", "model/stl", 3)
    {
        public override ValueTask<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default) =>
            new([1, 2, 3]);

        public override ValueTask<string> ReadAllTextAsync(CancellationToken cancellationToken = default) =>
            new("");
    }

    [Fact]
    public async Task TheLineTheWikiWrites_MapsTheEndpoint_AndTheServerRenderCannotSend()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Directory.CreateTempSubdirectory("eq-uploads-wiki-").FullName,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(ui => ui.UseUploads(uploads => uploads.Accept(Uploads.Models)));
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/_equantic/uploads");
        var answer = await app.GetTestClient().SendAsync(request);
        answer.Headers.GetValues("Tus-Version").Should().Contain("1.0.0");

        var uploads = app.Services.GetRequiredService<IUploads>();
        uploads.IsAvailable.Should().BeFalse();
        var upload = uploads.Start(new ChosenFile(), Uploads.Models);
        upload.State.Should().Be(UploadState.Failed);
        upload.Error.Should().Be("This page cannot upload a file until it runs in the browser.");
        upload.Size.Should().Be(3);
        upload.Result.Should().BeNull();
        await app.StopAsync();
    }

    /// <summary>The app's own storage, as the page's section on several instances writes one: here over
    /// the shared backing the instances' tests use.</summary>
    private sealed class BucketUploadStorage(SharedUploads bucket) : IUploadStorage
    {
        private readonly SharedUploadStorage _keeps = new(bucket);

        public ValueTask CreateAsync(StoredUpload upload, CancellationToken cancellationToken = default) => _keeps.CreateAsync(upload, cancellationToken);
        public ValueTask<StoredUpload?> FindAsync(string id, CancellationToken cancellationToken = default) => _keeps.FindAsync(id, cancellationToken);
        public ValueTask<long> LengthAsync(string id, CancellationToken cancellationToken = default) => _keeps.LengthAsync(id, cancellationToken);

        public ValueTask<long> AppendAsync(string id, long offset, Stream content, CancellationToken cancellationToken = default) =>
            _keeps.AppendAsync(id, offset, content, cancellationToken);

        public ValueTask<Stream> OpenReadAsync(string id, CancellationToken cancellationToken = default) => _keeps.OpenReadAsync(id, cancellationToken);
        public ValueTask DeleteAsync(string id, CancellationToken cancellationToken = default) => _keeps.DeleteAsync(id, cancellationToken);
        public IAsyncEnumerable<StoredUpload> ListAsync(CancellationToken cancellationToken = default) => _keeps.ListAsync(cancellationToken);
    }

    [Fact]
    public async Task TheStorageLineTheWikiWrites_KeepsTheUploadsInTheAppsStorage()
    {
        var bucket = new SharedUploads();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Directory.CreateTempSubdirectory("eq-uploads-wiki-").FullName,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(bucket);
        builder.Services.AddUI(ui => ui.UseUploads(uploads => uploads
            .Accept(Uploads.Models)
            .UseStorage<BucketUploadStorage>()));
        await using var app = builder.Build();
        app.MapUI();
        await app.StartAsync();

        var browser = new TusBrowser(app.GetTestClient());
        var location = await browser.UploadAsync([1, 2, 3], "bracket.stl", "models", partSize: 3);

        app.Services.GetRequiredService<IUploadStorage>().Should().BeOfType<BucketUploadStorage>();
        lock (bucket.Gate) bucket.Bytes[TusBrowser.IdOf(location)].Should().Equal(1, 2, 3);
        await app.StopAsync();
    }
}
