using System.Net;
using System.Text.Json;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>
/// The server receives files in resumable parts and hands them to a Server Action (#1214). Every case
/// sends real tus requests through the real endpoint and reads back what only exists when it worked:
/// the bytes an action read, the files in the folder.
/// </summary>
public sealed class UploadsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("eq-uploads-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static byte[] Content(int length) => Enumerable.Range(0, length).Select(i => (byte)(i * 31 + 7)).ToArray();

    private static object Argument(string location, string name = "forged.exe", long size = 1) => new
    {
        id = TusBrowser.IdOf(location),
        // What the page wrote beside the id, which the server never reads.
        name,
        mimeType = "application/x-forged",
        size = size.ToString(),
        policy = "small",
    };

    [Fact]
    public async Task AFileSentInParts_ReachesTheServerAction_ByteForByte()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var content = Content(2_500);

        var location = await browser.UploadAsync(content, "bracket.stl", "models", partSize: 1_000);

        (await browser.OffsetAsync(location)).Should().Be(content.Length);
        var (status, answer) = await browser.InvokeAsync("Receive", Argument(location));
        status.Should().Be(HttpStatusCode.OK, answer);
        // Every member is the server's record, not what the page wrote beside the id.
        answer.Should().Be($"{TusBrowser.IdOf(location)}|bracket.stl|model/stl|2500|models|{Convert.ToBase64String(content)}");
        app.Log.Runs.Should().Be(1);
    }

    [Fact]
    public async Task AVisitor_IsNamedByAStrictlyNecessaryCookie_WrittenBesideTheBytes()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();

        var location = await browser.UploadAsync(Content(10), "part.obj", "models", partSize: 10);

        browser.Cookie.Should().MatchRegex("^[0-9a-f]{32}$");
        browser.LastSetCookie.Should().ContainAll("httponly", "samesite=lax", "path=/", "max-age=86400")
            .And.NotContain("secure", "the request was not made over HTTPS");
        var record = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(_folder, TusBrowser.IdOf(location) + ".upload.json"))).RootElement;
        record.GetProperty("Owner").GetString().Should().Be($"visitor:{browser.Cookie}");
        record.GetProperty("Policy").GetString().Should().Be("models");
        record.GetProperty("Name").GetString().Should().Be("part.obj");

        // A second upload keeps the browser's id and writes the cookie again, so its lifetime is counted
        // from the latest upload it names.
        var cookie = browser.Cookie;
        browser.LastSetCookie = null;
        await browser.UploadAsync(Content(10), "next.obj", "models", partSize: 10);
        browser.Cookie.Should().Be(cookie);
        browser.LastSetCookie.Should().StartWith($"eq-uploads={cookie};").And.Contain("max-age=86400");
    }

    [Fact]
    public async Task OverHttps_TheVisitorsCookieIsSecure()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        app.Client.BaseAddress = new Uri("https://localhost/");
        var browser = app.Browser();

        using var created = await browser.CreateAsync(5, "small", "a.txt");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        browser.LastSetCookie.Should().Contain("secure");
    }

    [Fact]
    public async Task AnotherBrowser_CannotInspectContinueDeleteOrReceiveTheUpload()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var owner = app.Browser();
        var content = Content(100);
        var location = await owner.UploadAsync(content, "model.3mf", "models", partSize: 60, sendUpTo: 60);
        var other = app.Browser();
        using var others = await other.CreateAsync(5, "small", "mine.txt");
        other.Cookie.Should().NotBeNull().And.NotBe(owner.Cookie);

        using (var head = await other.HeadAsync(location)) head.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using (var patch = await other.PatchAsync(location, 60, content[60..]))
        {
            patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await patch.Content.ReadAsStringAsync()).Should().Be("This upload belongs to someone else.");
        }
        using (var delete = await other.DeleteAsync(location)) delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Nothing of it moved: the owner's offset is where the owner left it, and its bytes are there.
        (await owner.OffsetAsync(location)).Should().Be(60);
        app.FilesInFolder().Should().Contain(TusBrowser.IdOf(location));
        using (var patch = await owner.PatchAsync(location, 60, content[60..])) patch.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (status, answer) = await other.InvokeAsync("Receive", Argument(location));
        status.Should().Be(HttpStatusCode.Forbidden);
        answer.Should().Be("This upload belongs to someone else.");
        app.Log.Runs.Should().Be(0);
        (await owner.InvokeAsync("Receive", Argument(location))).Answer.Should().EndWith(Convert.ToBase64String(content));
    }

    [Fact]
    public async Task ASignedInPersonOwnsTheUpload_FromAnyBrowser_AndNobodyElseDoes()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var content = Content(40);
        var alice = app.Browser(user: "alice");
        var location = await alice.UploadAsync(content, "a.stl", "models", partSize: 40);
        alice.LastSetCookie.Should().BeNull("a signed-in person is named by the sign-in, not by a cookie");

        var bob = app.Browser(user: "bob");
        using (var head = await bob.HeadAsync(location)) head.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await bob.InvokeAsync("Receive", Argument(location))).Status.Should().Be(HttpStatusCode.Forbidden);

        var aliceElsewhere = app.Browser(user: "alice");
        var (status, answer) = await aliceElsewhere.InvokeAsync("Receive", Argument(location));
        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Should().EndWith(Convert.ToBase64String(content));
    }

    [Fact]
    public async Task AVisitorWhoSignsIn_GoesOnWithTheUploadTheyStarted()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var content = Content(100);
        var visitor = app.Browser();
        var location = await visitor.UploadAsync(content, "model.stl", "models", partSize: 60, sendUpTo: 60);

        // The same browser, signed in now: it carries the visitor's cookie and a sign-in.
        var signedIn = app.Browser(user: "carol");
        signedIn.Cookie = visitor.Cookie;
        using (var patch = await signedIn.PatchAsync(location, 60, content[60..]))
            patch.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (status, answer) = await signedIn.InvokeAsync("Receive", Argument(location));
        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Should().EndWith(Convert.ToBase64String(content));

        // The sign-in alone, in another browser, is not the browser that started it.
        var elsewhere = app.Browser(user: "carol");
        using (var head = await elsewhere.HeadAsync(location)) head.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AFileLargerThanItsPolicy_IsRefused_413()
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using var response = await app.Browser().CreateAsync(11, "small", "notes.txt");

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await response.Content.ReadAsStringAsync()).Should().Be("The file is larger than the 10 bytes accepted here.");
        app.FilesInFolder().Should().BeEmpty();
    }

    [Fact]
    public async Task AFormatThePolicyDoesNotAccept_IsRefused_415()
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using var response = await app.Browser().CreateAsync(10, "models", "setup.exe");

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await response.Content.ReadAsStringAsync()).Should().Be("Only .stl, .obj, .3mf files are accepted here.");
        app.FilesInFolder().Should().BeEmpty();
    }

    [Fact]
    public async Task AnExtensionIsComparedWithoutCase_AndAFolderTheDeviceSentIsDropped()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();

        var location = await browser.UploadAsync(Content(4), @"C:\Users\me\..\Bracket.STL", "models", partSize: 4);

        (await browser.InvokeAsync("Receive", Argument(location))).Answer.Should().Contain("|Bracket.STL|");
    }

    [Fact]
    public async Task TheUnfinishedUploadsOfOneOwner_AreLimited_429()
    {
        await using var app = await UploadsApp.StartAsync(_folder, configuration: new Dictionary<string, string?>
        {
            [$"{UploadsOptions.SectionName}:{nameof(UploadsOptions.MaxUnfinished)}"] = "2",
        });
        var browser = app.Browser();
        var first = await browser.UploadAsync(Content(10), "a.stl", "models", partSize: 5, sendUpTo: 5);
        await browser.UploadAsync(Content(10), "b.stl", "models", partSize: 5, sendUpTo: 5);

        using (var third = await browser.CreateAsync(10, "models", "c.stl"))
        {
            third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            (await third.Content.ReadAsStringAsync()).Should().Be("At most 2 uploads may be unfinished at a time.");
        }

        // Another person is not counted against this one.
        using (var others = await app.Browser().CreateAsync(10, "models", "c.stl"))
            others.StatusCode.Should().Be(HttpStatusCode.Created);

        // A finished upload no longer counts.
        using (var finished = await browser.PatchAsync(first, 5, Content(10)[5..]))
            finished.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var third = await browser.CreateAsync(10, "models", "c.stl"))
            third.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreationsSentAtOnce_CannotPassTheLimitTogether()
    {
        await using var app = await UploadsApp.StartAsync(_folder, configuration: new Dictionary<string, string?>
        {
            [$"{UploadsOptions.SectionName}:{nameof(UploadsOptions.MaxUnfinished)}"] = "3",
        });
        var browser = app.Browser(user: "carol");

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => browser.CreateAsync(10, "models", $"{i}.stl")));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(3);
        responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests).Should().Be(9);
        app.FilesInFolder().Count(file => file.EndsWith(".upload.json", StringComparison.Ordinal)).Should().Be(3);
        foreach (var response in responses) response.Dispose();
    }

    [Theory]
    [InlineData("shapes", "The app accepts no uploads under the policy 'shapes'.")]
    [InlineData(null, "The upload must name its policy.")]
    public async Task AnUploadUnderNoPolicyTheAppAccepts_IsRefused(string? policy, string reason)
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using var response = await app.Browser().CreateAsync(10, policy, "a.stl");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Be(reason);
        app.FilesInFolder().Should().BeEmpty();
    }

    [Fact]
    public async Task AnUploadWithoutAFileName_IsRefused()
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using var response = await app.Browser().CreateAsync(10, "small", fileName: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Be("The upload must name its file.");
    }

    [Fact]
    public async Task AnUploadWhoseSizeIsNotKnown_IsRefused()
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using var response = await app.Browser().CreateAsync(0, "small", "a.txt", deferLength: true);

        // The engine refuses it itself, since the endpoint does not offer the extension: a policy weighs
        // the size when the upload starts.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        app.FilesInFolder().Should().BeEmpty();
    }

    [Fact]
    public async Task APartialUploadForAConcatenation_IsRefused()
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using var response = await app.Browser().CreateAsync(5, "small", "a.txt", concatenation: "partial");

        // The endpoint does not offer the extension, so the engine takes no such request, and nothing
        // under its path answers one.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
        app.FilesInFolder().Should().BeEmpty();
    }

    [Fact]
    public async Task APolicyThatRequiresSigningIn_RefusesAVisitor_401_AndTakesASignedInPerson()
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        using (var visitor = await app.Browser().CreateAsync(10, "members", "a.txt"))
        {
            visitor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await visitor.Content.ReadAsStringAsync()).Should().Be("Sign in to upload this file.");
        }

        app.FilesInFolder().Should().BeEmpty();
        using var member = await app.Browser(user: "dave").CreateAsync(10, "members", "a.txt");
        member.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AnUnfinishedUpload_IsNotBound_409_AndTheActionDoesNotRun()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var location = await browser.UploadAsync(Content(20), "a.stl", "models", partSize: 10, sendUpTo: 10);

        var (status, answer) = await browser.InvokeAsync("Receive", Argument(location));

        status.Should().Be(HttpStatusCode.Conflict);
        answer.Should().Be("The upload is not finished yet.");
        app.Log.Runs.Should().Be(0);
    }

    [Fact]
    public async Task AnIdTheServerNeverGave_IsNotBound_404()
    {
        await using var app = await UploadsApp.StartAsync(_folder);

        var (status, answer) = await app.Browser().InvokeAsync("Receive", new { id = "0123456789abcdef0123456789abcdef" });

        status.Should().Be(HttpStatusCode.NotFound);
        answer.Should().Be("There is no upload with this id: it was never made, or it expired.");
        app.Log.Runs.Should().Be(0);
    }

    [Fact]
    public async Task AServerStartedOverTheSameFolder_StillKnowsWhoseEachUploadIs()
    {
        var content = Content(300);
        string location;
        string? cookie;
        await using (var before = await UploadsApp.StartAsync(_folder))
        {
            var browser = before.Browser();
            location = await browser.UploadAsync(content, "a.stl", "models", partSize: 100, sendUpTo: 200);
            cookie = browser.Cookie;
        }

        await using var after = await UploadsApp.StartAsync(_folder);
        var stranger = after.Browser();
        using (var head = await stranger.HeadAsync(location)) head.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var owner = after.Browser();
        owner.Cookie = cookie;
        (await owner.OffsetAsync(location)).Should().Be(200);
        using (var patch = await owner.PatchAsync(location, 200, content[200..])) patch.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (status, answer) = await owner.InvokeAsync("Receive", Argument(location));
        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Should().EndWith(Convert.ToBase64String(content));
    }

    [Fact]
    public async Task TheCleaner_RemovesAnExpiredUpload_FinishedOrNot()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var finished = await browser.UploadAsync(Content(10), "done.stl", "models", partSize: 10);
        var unfinished = await browser.UploadAsync(Content(10), "half.stl", "models", partSize: 5, sendUpTo: 5);
        app.FilesInFolder().Should().Contain([TusBrowser.IdOf(finished), TusBrowser.IdOf(unfinished)]);

        // A day and the sweep's interval later, the cleaner's next sweep finds both past their expiry.
        app.Time.Advance(TimeSpan.FromHours(24) + ExpiredUploadsCleaner.Interval);
        await Until(() => app.FilesInFolder().Count == 0);

        app.FilesInFolder().Should().BeEmpty("the bytes, the engine's files and the records are all gone");
        (await browser.InvokeAsync("Receive", Argument(finished))).Status.Should().Be(HttpStatusCode.NotFound);
        using var head = await browser.HeadAsync(unfinished);
        head.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheCleaner_KeepsAnUploadBeforeItsExpiry()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var location = await browser.UploadAsync(Content(10), "done.stl", "models", partSize: 10);

        app.Time.Advance(TimeSpan.FromHours(23));
        await Task.Delay(200);

        (await browser.InvokeAsync("Receive", Argument(location))).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnActionThatDeletesTheFile_EmptiesTheFolder()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var location = await browser.UploadAsync(Content(10), "done.stl", "models", partSize: 10);

        var (status, answer) = await browser.InvokeAsync("Keep", Argument(location));

        status.Should().Be(HttpStatusCode.OK, answer);
        answer.Should().Be("done.stl");
        app.FilesInFolder().Should().BeEmpty();
        (await browser.InvokeAsync("Receive", Argument(location))).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheOwnerDeletesTheUpload_OverTheProtocol()
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var browser = app.Browser();
        var location = await browser.UploadAsync(Content(10), "a.stl", "models", partSize: 5, sendUpTo: 5);

        using (var delete = await browser.DeleteAsync(location)) delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        app.FilesInFolder().Should().BeEmpty("the bytes and the record are both gone");
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    public async Task ARequestFromAnotherSite_IsRefused(string method)
    {
        await using var app = await UploadsApp.StartAsync(_folder);
        var owner = app.Browser();
        var location = await owner.UploadAsync(Content(10), "a.stl", "models", partSize: 5, sendUpTo: 5);
        var evil = app.Browser(origin: "https://evil.example");
        evil.Cookie = owner.Cookie;

        using var response = method switch
        {
            "POST" => await evil.CreateAsync(10, "models", "b.stl"),
            "PATCH" => await evil.PatchAsync(location, 5, Content(10)[5..]),
            _ => await evil.HeadAsync(location),
        };

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        app.FilesInFolder().Count(file => file.EndsWith(".upload.json", StringComparison.Ordinal)).Should().Be(1);
        (await owner.OffsetAsync(location)).Should().Be(5);
    }

    [Fact]
    public async Task AnOriginTheAppAllowsForItsActions_MayUploadToo()
    {
        await using var app = await UploadsApp.StartAsync(_folder, configuration: new Dictionary<string, string?>
        {
            [$"{ServerActionsOptions.SectionName}:AllowedOrigins:0"] = "https://admin.example",
        });

        using var response = await app.Browser(origin: "https://admin.example").CreateAsync(10, "models", "b.stl");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AnUploadedFileThePageBuilt_HoldsNoContent()
    {
        var file = new UploadedFile("0123456789abcdef0123456789abcdef", "a.stl", "model/stl", 10, "models");

        await FluentActions.Awaiting(() => file.OpenRead()).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => file.Delete()).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AnActionThatTakesAFile_InAnAppWithoutUploads_SaysSo()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ReceivedLog>();
        builder.Services.AddUI(options => options.ScanAssembly(typeof(UploadsTests).Assembly));
        await using var app = builder.Build();
        app.UseServerActions();
        await app.StartAsync();

        var (status, answer) = await new TusBrowser(app.GetTestClient()).InvokeAsync("Receive", new { id = "x" });

        status.Should().Be(HttpStatusCode.InternalServerError);
        answer.Should().Be("The app receives no uploads: call UseUploads on AddUI.");
    }

    [Theory]
    [InlineData(nameof(UploadsOptions.MaxUnfinished), "0")]
    [InlineData(nameof(UploadsOptions.Expiry), "00:00:00")]
    [InlineData(nameof(UploadsOptions.Expiry), "401.00:00:00")]
    public async Task ALimitTheUploadsCannotRunWith_StopsTheApp(string setting, string value)
    {
        var start = () => UploadsApp.StartAsync(_folder, configuration: new Dictionary<string, string?>
        {
            [$"{UploadsOptions.SectionName}:{setting}"] = value,
        });

        (await start.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain($"{UploadsOptions.SectionName}:{setting}");
    }

    [Fact]
    public void TwoPoliciesOfOneName_AreRefused_AndOnePolicyTwiceIsAcceptedOnce()
    {
        var services = new ServiceCollection();

        var accept = () => services.AddUI(options => options.UseUploads(uploads => uploads
            .Accept(UploadsApp.Models)
            .Accept(UploadsApp.Models)
            .Accept(new UploadPolicy("models", [], 1))));

        accept.Should().Throw<ArgumentException>().WithMessage("*'models'*");
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var waited = 0; !condition() && waited < 10_000; waited += 50) await Task.Delay(50);
    }
}
