using System.Net;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>
/// What bounds the uploads a server keeps (#1287), and what the server, not the device, says of each one
/// (#1293). Every case goes through the real tus endpoint and the action that receives the file.
/// </summary>
public sealed class UploadsLimitsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("eq-uploads-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static byte[] Content(int length) => Enumerable.Range(0, length).Select(i => (byte)(i * 31 + 7)).ToArray();

    private Task<UploadsApp> Start(string setting, string value) =>
        UploadsApp.StartAsync(_folder, configuration: new Dictionary<string, string?>
        {
            [$"{UploadsOptions.SectionName}:{setting}"] = value,
        });

    [Fact]
    public async Task AScriptThatNeverKeepsTheCookie_StopsAtTheStoresCount_429()
    {
        await using var app = await Start(nameof(UploadsOptions.MaxTotalUploads), "3");

        // Each browser sends no cookie: a new owner every time, whom no per-owner limit ever reaches.
        for (var i = 0; i < 3; i++) await app.Browser().UploadAsync(Content(5), $"{i}.txt", "small", partSize: 5);
        using var fourth = await app.Browser().CreateAsync(5, "small", "3.txt");

        fourth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await fourth.Content.ReadAsStringAsync()).Should().Be("The server keeps at most 3 uploads at a time: try again later.");
        app.FilesInFolder().Count(file => file.EndsWith(".upload.json", StringComparison.Ordinal)).Should().Be(3);
    }

    [Fact]
    public async Task TheStoresBytes_AreBoundedToo_413()
    {
        // The quota is the largest policy's size, the least the app may start with.
        await using var app = await Start(nameof(UploadsOptions.MaxTotalBytes), "1048576");
        await app.Browser().UploadAsync(Content(1_048_570), "a.stl", "models", partSize: 1_048_570);

        using var next = await app.Browser().CreateAsync(10, "small", "b.txt");

        next.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await next.Content.ReadAsStringAsync()).Should().Be("The server's uploads may take at most 1048576 bytes: try again later.");
    }

    [Fact]
    public async Task AFinishedUpload_CountsAgainstItsOwner_UntilItIsGone()
    {
        await using var app = await Start(nameof(UploadsOptions.MaxUploadsPerOwner), "2");
        var browser = app.Browser();
        var first = await browser.UploadAsync(Content(5), "a.txt", "small", partSize: 5);
        await browser.UploadAsync(Content(5), "b.txt", "small", partSize: 5);

        using (var third = await browser.CreateAsync(5, "small", "c.txt"))
        {
            third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            (await third.Content.ReadAsStringAsync()).Should().Be("At most 2 uploads may be kept at a time.");
        }

        using (var deleted = await browser.DeleteAsync(first)) deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var third = await browser.CreateAsync(5, "small", "c.txt")) third.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AnOwnersBytes_AreBounded_413()
    {
        await using var app = await Start(nameof(UploadsOptions.MaxBytesPerOwner), "1048576");
        var browser = app.Browser();
        await browser.UploadAsync(Content(1_048_570), "a.stl", "models", partSize: 1_048_570);

        using var next = await browser.CreateAsync(10, "small", "b.txt");

        next.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await next.Content.ReadAsStringAsync()).Should().Be("Your uploads may take at most 1048576 bytes.");
        using (var others = await app.Browser().CreateAsync(5, "small", "b.txt")) others.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(nameof(UploadsOptions.MaxUploadsPerOwner), "0")]
    [InlineData(nameof(UploadsOptions.MaxBytesPerOwner), "0")]
    [InlineData(nameof(UploadsOptions.MaxTotalUploads), "0")]
    [InlineData(nameof(UploadsOptions.MaxTotalBytes), "0")]
    public async Task AQuotaOfNothing_StopsTheApp(string setting, string value)
    {
        var start = () => Start(setting, value);

        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain($"{UploadsOptions.SectionName}:{setting}");
    }

    [Fact]
    public async Task TheType_IsTheServers_ReadFromTheExtension_NeverTheDevices()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        using var created = await browser.CreateAsync(5, "small", "page.png", fileType: "text/html");
        var location = created.Headers.Location!.ToString();
        using (var sent = await browser.PatchAsync(location, 0, Content(5))) sent.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (status, answer) = await browser.InvokeAsync("Receive", new { id = TusBrowser.IdOf(location) });

        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Split('|')[2].Should().Be("image/png");
    }

    [Theory]
    [InlineData("a\u0001b<c>.txt", "ab_c_.txt")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("lpt1", "_lpt1")]
    [InlineData("report. . ", "report")]
    [InlineData("invoice\u202Efdp.txt", "invoicefdp.txt")]
    public async Task TheName_IsOneAFileSystemTakesAsItIs(string sent, string kept)
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var location = await browser.UploadAsync(Content(5), sent, "small", partSize: 5);

        var (status, answer) = await browser.InvokeAsync("Receive", new { id = TusBrowser.IdOf(location) });

        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Split('|')[1].Should().Be(kept);
    }

    [Fact]
    public async Task AnEmptyFile_FinishedAsItIsCreated_IsNotCountedUnfinished()
    {
        await using var app = await Start(nameof(UploadsOptions.MaxUnfinished), "1");
        var browser = app.Browser();
        using (var first = await browser.CreateAsync(0, "small", "empty.txt")) first.StatusCode.Should().Be(HttpStatusCode.Created);

        using var second = await browser.CreateAsync(5, "small", "next.txt");

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ANameWithALongExtension_IsCut_NotFailed()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var location = await browser.UploadAsync(Content(5), "a." + new string('x', 300), "small", partSize: 5);

        var (status, answer) = await browser.InvokeAsync("Receive", new { id = TusBrowser.IdOf(location) });

        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Split('|')[1].Should().HaveLength(255).And.StartWith("a.xxx");
    }

    [Fact]
    public async Task APolicyLargerThanTheByteQuota_StopsTheApp()
    {
        // The models policy accepts larger files than this quota allows.
        var start = () => Start(nameof(UploadsOptions.MaxBytesPerOwner), "8");

        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain("The upload policy 'models'").And.Contain("MaxBytesPerOwner");
    }

    [Theory]
    [InlineData("http://192.168.1.20/")]
    [InlineData("http://10.0.0.5/")]
    public async Task OverPlainHttpToTheLocalNetwork_TheCookieIsNotSecure_SoAPhoneOnTheDevServerKeepsIt(string address)
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        app.Client.BaseAddress = new Uri(address);
        var browser = app.Browser();

        using var created = await browser.CreateAsync(5, "small", "a.txt");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        browser.LastSetCookie.Should().NotContain("secure");
    }

    [Theory]
    [InlineData("..")]
    [InlineData("folder/..")]
    [InlineData("\u0001\u0002")]
    public async Task ANameThatIsNoFile_IsRefused(string sent)
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using var response = await app.Browser().CreateAsync(5, "small", sent);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Be("The upload must name its file.");
    }

    [Fact]
    public async Task AFolderUnderTheWebRoot_StopsTheApp()
    {
        var webRoot = Directory.CreateTempSubdirectory("eq-uploads-web-").FullName;
        try
        {
            var start = () => UploadsApp.StartAsync(Path.Combine(webRoot, "uploads"), webRoot: webRoot);

            (await start.Should().ThrowAsync<OptionsValidationException>())
                .Which.Message.Should().Contain("under the web root");
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task OverPlainHttpToAnotherHost_TheVisitorsCookieIsStillSecure()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        app.Client.BaseAddress = new Uri("http://app.example/");
        var browser = app.Browser();

        using var created = await browser.CreateAsync(5, "small", "a.txt");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        browser.LastSetCookie.Should().Contain("secure");
    }
}
