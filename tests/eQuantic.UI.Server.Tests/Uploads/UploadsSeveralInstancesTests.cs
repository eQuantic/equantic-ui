using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>
/// Uploads behind a balancer (#1886): each request of an upload, and the Server Action that receives it,
/// may reach another instance of the app. Two apps over one <see cref="SharedUploadStorage"/> stand for
/// two instances; every case sends real tus requests through the real endpoints and reads back what only
/// exists when it worked, the bytes an action read.
/// </summary>
public sealed class UploadsSeveralInstancesTests : IDisposable
{
    private readonly string _firstFolder = Directory.CreateTempSubdirectory("eq-uploads-a-").FullName;
    private readonly string _secondFolder = Directory.CreateTempSubdirectory("eq-uploads-b-").FullName;

    public void Dispose()
    {
        Directory.Delete(_firstFolder, recursive: true);
        Directory.Delete(_secondFolder, recursive: true);
    }

    private static byte[] Content(int length) => Enumerable.Range(0, length).Select(i => (byte)(i * 31 + 7)).ToArray();

    [Fact]
    public async Task AnUploadCreatedOnOneInstance_IsSentToAndReceivedOnAnother_ByteForByte()
    {
        var shared = new SharedUploads();
        await using var first = await UploadsApp.StartAsync(_firstFolder, shared: shared);
        await using var second = await UploadsApp.StartAsync(_secondFolder, shared: shared);
        var onFirst = first.Browser(user: "ana");
        var onSecond = second.Browser(user: "ana");
        var content = Content(2_500);

        using var created = await onFirst.CreateAsync(content.Length, "models", "bracket.stl");
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var location = created.Headers.Location!.ToString();
        // Each part reaches the instance a balancer would choose, the other one each time.
        for (var (offset, part) = (0, 0); offset < content.Length; offset += 1_000, part++)
        {
            var browser = part % 2 == 0 ? onSecond : onFirst;
            using var patched = await browser.PatchAsync(location, offset, content[offset..Math.Min(content.Length, offset + 1_000)]);
            patched.StatusCode.Should().Be(HttpStatusCode.NoContent, await patched.Content.ReadAsStringAsync());
        }

        (await onFirst.OffsetAsync(location)).Should().Be(content.Length);
        var (status, answer) = await onSecond.InvokeAsync("Receive", new { id = TusBrowser.IdOf(location) });
        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Should().StartWith($"{TusBrowser.IdOf(location)}|bracket.stl|").And.EndWith($"|2500|models|{Convert.ToBase64String(content)}");
        second.Log.Runs.Should().Be(1);
        // Neither instance kept anything of it in a folder of its own.
        first.FilesInFolder().Should().BeEmpty();
        second.FilesInFolder().Should().BeEmpty();
    }

    [Fact]
    public async Task AnUploadIsItsOwnersAlone_OnEveryInstance()
    {
        var shared = new SharedUploads();
        await using var first = await UploadsApp.StartAsync(_firstFolder, shared: shared);
        await using var second = await UploadsApp.StartAsync(_secondFolder, shared: shared);
        var visitor = first.Browser();
        var location = await visitor.UploadAsync(Content(100), "bracket.stl", "models", partSize: 100, sendUpTo: 60);

        // The visitor's cookie names the owner on the other instance too; a stranger is refused there.
        var sameVisitor = new TusBrowser(second.Client) { Cookie = visitor.Cookie };
        (await sameVisitor.OffsetAsync(location)).Should().Be(60);
        using (var stranger = await second.Browser().HeadAsync(location))
            stranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheLimits_AreCountedFromTheStorage_NotFromOneInstance()
    {
        var shared = new SharedUploads();
        var limit = new Dictionary<string, string?> { [$"{UploadsOptions.SectionName}:{nameof(UploadsOptions.MaxUploadsPerOwner)}"] = "2" };
        await using var first = await UploadsApp.StartAsync(_firstFolder, configuration: limit, shared: shared);
        await using var second = await UploadsApp.StartAsync(_secondFolder, configuration: limit, shared: shared);

        using (var one = await first.Browser(user: "bea").CreateAsync(10, "models", "a.stl"))
            one.StatusCode.Should().Be(HttpStatusCode.Created);
        using (var two = await second.Browser(user: "bea").CreateAsync(10, "models", "b.stl"))
            two.StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (var instance in new[] { first, second })
        {
            using var third = await instance.Browser(user: "bea").CreateAsync(10, "models", "c.stl");
            third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            (await third.Content.ReadAsStringAsync()).Should().Be("At most 2 uploads may be kept at a time.");
        }
    }

    [Fact]
    public async Task TheCleaner_RemovesAnExpiredUpload_FromTheStorage()
    {
        var shared = new SharedUploads();
        await using var app = await UploadsApp.StartAsync(_firstFolder, shared: shared);
        var browser = app.Browser(user: "ana");
        var location = await browser.UploadAsync(Content(10), "a.stl", "models", partSize: 10);

        app.Time.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(10));
        await Until(() =>
        {
            lock (shared.Gate) return shared.Records.Count == 0 && shared.Bytes.Count == 0;
        });

        var (status, _) = await browser.InvokeAsync("Receive", new { id = TusBrowser.IdOf(location) });
        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/_equantic/uploads/..%2F..%2Fsecret")]
    [InlineData("/api/_equantic/uploads/0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("/api/_equantic/uploads/0123456789abcdef0123456789abcdef0")]
    public async Task AnIdTheServerNeverGives_IsNotFound_AndNeverReachesTheStorage(string address)
    {
        var shared = new SharedUploads();
        await using var app = await UploadsApp.StartAsync(_firstFolder, shared: shared);
        var browser = app.Browser(user: "ana");

        using (var head = await browser.HeadAsync(address)) head.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using (var patch = await browser.PatchAsync(address, 0, [1, 2, 3])) patch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var id = address[(address.LastIndexOf('/') + 1)..];
        var (status, _) = await browser.InvokeAsync("Receive", new { id = Uri.UnescapeDataString(id) });
        status.Should().Be(HttpStatusCode.NotFound);

        shared.Asked.Should().BeEmpty("only an id the server gives is handed to a storage");
    }

    [Fact]
    public async Task TheDefaultStorage_AppendsOnlyWhereTheContentStands()
    {
        await using var app = await UploadsApp.StartAsync(_firstFolder);
        var storage = app.Services.GetRequiredService<IUploadStorage>();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await storage.CreateAsync(new StoredUpload(id, "user:ana", "models", "a.stl", "model/stl", 15, now, now.AddDays(1)));

        (await storage.AppendAsync(id, 0, new MemoryStream(Content(10)))).Should().Be(10);
        (await storage.AppendAsync(id, 5, new MemoryStream(Content(5)))).Should().Be(0, "the offset 5 was already passed");

        (await storage.LengthAsync(id)).Should().Be(10);
        await using var read = await storage.OpenReadAsync(id);
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        copy.ToArray().Should().Equal(Content(10));
    }

    [Fact]
    public async Task AnAppWithItsOwnStorage_DoesNotReadTheFolder_EvenUnderTheWebRoot()
    {
        var webRoot = Directory.CreateTempSubdirectory("eq-uploads-web-").FullName;
        try
        {
            await using var app = await UploadsApp.StartAsync(Path.Combine(webRoot, "uploads"), webRoot: webRoot, shared: new SharedUploads());
            using var created = await app.Browser(user: "ana").CreateAsync(10, "models", "a.stl");
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            Directory.Exists(Path.Combine(webRoot, "uploads")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TheDefaultStorage_RemovesTheSideFilesAnEarlierEngineKept()
    {
        await using var app = await UploadsApp.StartAsync(_firstFolder);
        var storage = app.Services.GetRequiredService<IUploadStorage>();
        var id = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await storage.CreateAsync(new StoredUpload(id, "user:ana", "models", "a.stl", "model/stl", 10, now, now.AddDays(1)));
        foreach (var side in new[] { ".uploadlength", ".metadata", ".expiration", ".chunkstart", ".chunkcomplete" })
            await File.WriteAllTextAsync(Path.Combine(_firstFolder, id + side), "1");

        await storage.DeleteAsync(id);

        Directory.EnumerateFiles(_firstFolder).Should().BeEmpty();
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var tries = 0; tries < 200 && !condition(); tries++) await Task.Delay(25);
        condition().Should().BeTrue();
    }
}
