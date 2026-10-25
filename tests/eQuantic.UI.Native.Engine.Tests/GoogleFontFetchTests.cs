using System.Net;
using System.Security.Cryptography;
using System.Text;
using eQuantic.UI.Build;
using eQuantic.UI.Tests;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A family from Google Fonts is fetched by the build, pinned in <c>fonts.lock.json</c> by its URL and the
/// hash of its bytes, and kept out of the source tree (#1057). Whatever would change the app's text without
/// the app choosing it fails the build, naming the way to renew. Google is played by a handler that answers
/// as the real API was measured to answer.
/// </summary>
public sealed class GoogleFontFetchTests : IDisposable
{
    private const string Place = "/app/Program.cs(3,12)";

    private readonly string _dir = Directory.CreateTempSubdirectory("eq-google-fetch-").FullName;
    private readonly FakeGoogle _google = new();

    private string Lock => Path.Combine(_dir, FontLock.FileName);
    private string Store => Path.Combine(_dir, "obj", "equantic", "fonts");
    private string Folder => FolderOf("Debug");

    /// <summary>The folder the build of one configuration embeds from: each build has its own.</summary>
    private string FolderOf(string configuration) => Path.Combine(_dir, "obj", configuration, "net10.0", "equantic", "fonts", "google");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task AFamily_IsFetchedPinnedAndKeptOutOfTheSourceTree()
    {
        var errors = await Fetch(Declared("Inter", Cut(400), Cut(700), Cut(400, italic: true)));

        errors.Should().BeEmpty();
        Directory.GetFiles(Folder).Select(Path.GetFileName).Should().BeEquivalentTo("Inter-400.ttf", "Inter-700.ttf", "Inter-400-italic.ttf");
        var bold = File.ReadAllBytes(Path.Combine(Folder, "Inter-700.ttf"));
        bold.Should().Equal(_google.FileOf("Inter", Cut(700)));
        OpenTypeFace.Read(bold).Should().Be(new OpenTypeFace("Inter", 700, 700, false), "the catalog reads the family from the file");
        var pins = FontLock.Read(Lock).Of(GoogleFontFetch.Provider, "Inter");
        pins.Keys.Should().BeEquivalentTo(new[] { Cut(400), Cut(700), Cut(400, italic: true) });
        pins[Cut(700)].Should().Be(new FontLock.Pin(_google.UrlOf("Inter", Cut(700)), Sha256(bold)));
        _google.Asked.Count(FakeGoogle.IsApi).Should().Be(1, "a family is asked for once, every cut together");
    }

    [Fact]
    public async Task ARebuild_AsksGoogleForNothing()
    {
        var inter = Declared("Inter", Cut(400), Cut(700));
        await Fetch(inter);
        _google.Asked.Clear();

        (await Fetch(inter)).Should().BeEmpty();

        _google.Asked.Should().BeEmpty();
    }

    /// <summary>A pinned file is never asked of the API again: one gone from the store comes back from the
    /// URL it was pinned at, whatever Google serves now, and one gone from a build's folder alone comes back
    /// from the store, asking nobody.</summary>
    [Fact]
    public async Task APinnedFileGoneFromTheStore_ComesBackFromItsPinnedUrl()
    {
        var inter = Declared("Inter", Cut(400));
        await Fetch(inter);
        var pinned = File.ReadAllBytes(Path.Combine(Folder, "Inter-400.ttf"));
        File.Delete(Path.Combine(Folder, "Inter-400.ttf"));
        _google.Asked.Clear();

        (await Fetch(inter)).Should().BeEmpty();
        _google.Asked.Should().BeEmpty("the store still holds it");

        ForgetStored("Inter", Cut(400));
        File.Delete(Path.Combine(Folder, "Inter-400.ttf"));
        _google.Replace("Inter");

        (await Fetch(inter)).Should().BeEmpty();

        _google.Asked.Should().Equal(_google.UrlOf("Inter", Cut(400), version: 1));
        File.ReadAllBytes(Path.Combine(Folder, "Inter-400.ttf")).Should().Equal(pinned);
    }

    [Fact]
    public async Task AFileThatChangedAtItsPinnedUrl_FailsTheBuild_UntilItsFamilyIsRenewed()
    {
        var inter = Declared("Inter", Cut(400));
        await Fetch(inter);
        var pinnedLock = File.ReadAllText(Lock);
        ForgetStored("Inter", Cut(400));
        var changed = TestFont.Build("Inter", advance: 777);
        _google.Changed[_google.UrlOf("Inter", Cut(400))] = changed;

        var errors = await Fetch(inter);

        errors.Should().ContainSingle().Which.Should().StartWith($"{Place}: error EQ4009: ")
            .And.Contain($"is not the one {FontLock.FileName} pins: its SHA-256 is {Sha256(changed)}")
            .And.Contain($"Take 'Inter' out of {FontLock.FileName}");
        File.ReadAllText(Lock).Should().Be(pinnedLock, "a family that fails keeps the pins it had");

        File.Delete(Lock);
        (await Fetch(inter)).Should().BeEmpty();
        FontLock.Read(Lock).Of(GoogleFontFetch.Provider, "Inter")[Cut(400)].Sha256.Should().Be(Sha256(changed));
    }

    [Fact]
    public async Task APinnedUrlGoogleNoLongerServes_FailsTheBuild()
    {
        var inter = Declared("Inter", Cut(400));
        await Fetch(inter);
        ForgetStored("Inter", Cut(400));
        _google.Gone.Add(_google.UrlOf("Inter", Cut(400)));

        var errors = await Fetch(inter);

        errors.Should().ContainSingle().Which.Should().StartWith($"{Place}: error EQ4009: Google Fonts no longer serves {_google.UrlOf("Inter", Cut(400))}");
    }

    /// <summary>One family never mixes two of Google's versions: a cut added to a family Google has
    /// replaced since its cuts were pinned waits for the family to be renewed whole.</summary>
    [Fact]
    public async Task ACutAddedToAFamilyGoogleReplaced_FailsTheBuild_AndTheFamilyKeepsItsPins()
    {
        await Fetch(Declared("Inter", Cut(400)));
        var pinnedLock = File.ReadAllText(Lock);
        _google.Replace("Inter");

        var errors = await Fetch(Declared("Inter", Cut(400), Cut(700)));

        errors.Should().ContainSingle().Which.Should().StartWith($"{Place}: error EQ4009: Google Fonts has replaced 'Inter' since its cuts were pinned");
        File.ReadAllText(Lock).Should().Be(pinnedLock);
        File.Exists(Path.Combine(Folder, "Inter-400.ttf")).Should().BeTrue("the pinned file stays where the build embeds it from");
    }

    /// <summary>A cut added by one build is checked against every pin of its family, those another build
    /// declared included: a Release build that declares only Bold after Google replaced a family whose
    /// Regular a Debug build pinned would otherwise pin one family at two of Google's versions (Copilot's
    /// second review of #1085).</summary>
    [Fact]
    public async Task CutsTwoBuildsDeclareApart_NeverPinTwoVersionsOfAFamily()
    {
        await Fetch(FolderOf("Debug"), Declared("Inter", Cut(400)));
        var pinnedLock = File.ReadAllText(Lock);
        _google.Replace("Inter");

        var errors = await Fetch(FolderOf("Release"), Declared("Inter", Cut(700)));

        errors.Should().ContainSingle().Which.Should().StartWith($"{Place}: error EQ4009: Google Fonts has replaced 'Inter' since its cuts were pinned");
        File.ReadAllText(Lock).Should().Be(pinnedLock);
    }

    [Fact]
    public async Task ACutTheLockPinsThatGoogleNoLongerHas_FailsAsAReplacedFamily()
    {
        await Fetch(Declared("Inter", Cut(400), Cut(700)));
        _google.Drop("Inter", Cut(700));

        var errors = await Fetch(Declared("Inter", Cut(400), Cut(400, italic: true)));

        errors.Should().ContainSingle().Which.Should()
            .StartWith($"{Place}: error EQ4009: Google Fonts has replaced 'Inter' since its cuts were pinned: Google Fonts has no Bold of 'Inter'")
            .And.Contain($"Take 'Inter' out of {FontLock.FileName}");
    }

    [Fact]
    public async Task AFamilyGoogleDoesNotHave_FailsNamingIt()
    {
        var errors = await Fetch(Declared("Intr", Cut(400)));

        errors.Should().Equal($"{Place}: error EQ4008: Google Fonts has no family named 'Intr'.");
    }

    [Fact]
    public async Task ACutGoogleDoesNotHave_FailsNamingIt()
    {
        var errors = await Fetch(Declared("Bebas Neue", Cut(400), Cut(700), Cut(400, italic: true)));

        errors.Should().Equal($"{Place}: error EQ4008: Google Fonts has no Bold, Regular italic of 'Bebas Neue'.");
    }

    /// <summary>A file is checked before it is pinned: a pin made of a web font would fail every build
    /// after it, far from its cause, and is never written.</summary>
    [Fact]
    public async Task AFileThatIsNotAFace_IsNeverPinned()
    {
        _google.Changed[_google.UrlOf("Inter", Cut(400))] = [.. "wOF2"u8, .. new byte[64]];

        var errors = await Fetch(Declared("Inter", Cut(400)));

        errors.Should().ContainSingle().Which.Should().StartWith($"{Place}: error EQ4008: Google Fonts served the Regular of 'Inter'")
            .And.Contain("it is a web font");
        File.Exists(Lock).Should().BeFalse();
    }

    [Fact]
    public async Task GoogleOutOfReach_FailsSayingSo()
    {
        _google.Unreachable = true;

        var errors = await Fetch(Declared("Inter", Cut(400)));

        errors.Should().ContainSingle().Which.Should().StartWith($"{Place}: error EQ4008: Google Fonts could not be reached to ask for 'Inter'");
    }

    /// <summary>A build that fails leaves the lock and its folder as they were, every file of it: what it
    /// fetched for a family that did not fail waits in the store, where no build embeds it from (Copilot's
    /// review of #1085). A family whose name was mistyped for one build keeps its pins, rather than being
    /// asked of Google again, perhaps at a new version, once the name is put right.</summary>
    [Fact]
    public async Task ABuildThatFails_LeavesTheLockAndTheFolderAsTheyWere()
    {
        await Fetch(Declared("Inter", Cut(400)));
        var pinnedLock = File.ReadAllText(Lock);
        var folder = Contents(Folder);

        var errors = await Fetch(Declared("Intr", Cut(400)), Declared("JetBrains Mono", Cut(400)));

        errors.Should().ContainSingle().Which.Should().Contain("no family named 'Intr'");
        File.ReadAllText(Lock).Should().Be(pinnedLock);
        Contents(Folder).Should().BeEquivalentTo(folder);

        _google.Replace("Inter");
        _google.Asked.Clear();
        (await Fetch(Declared("Inter", Cut(400)))).Should().BeEmpty();
        _google.Asked.Should().BeEmpty("the family put right is still pinned, and its file still there");
    }

    /// <summary>
    /// The families a build reads depend on its symbols: a Release build that does not see a family declared
    /// under <c>#if DEBUG</c> leaves its pins alone, and the next Debug build fetches nothing (Copilot's
    /// review of #1085). A family stays pinned until the app takes it out of the lock, and a build embeds
    /// only what it declares.
    /// </summary>
    [Fact]
    public async Task AFamilyABuildDoesNotDeclare_KeepsItsPins_AndIsNotLaidOut()
    {
        await Fetch(Declared("Inter", Cut(400)), Declared("JetBrains Mono", Cut(400)));

        (await Fetch(FolderOf("Release"), Declared("JetBrains Mono", Cut(400)))).Should().BeEmpty();
        FontLock.Read(Lock).Of(GoogleFontFetch.Provider, "Inter").Keys.Should().Equal(Cut(400));
        Directory.GetFiles(FolderOf("Release")).Select(Path.GetFileName).Should().Equal("JetBrainsMono-400.ttf");

        _google.Replace("Inter");
        _google.Asked.Clear();
        (await Fetch(Declared("Inter", Cut(400)), Declared("JetBrains Mono", Cut(400)))).Should().BeEmpty();
        _google.Asked.Should().BeEmpty();
        Directory.GetFiles(Folder).Select(Path.GetFileName).Should().BeEquivalentTo("Inter-400.ttf", "JetBrainsMono-400.ttf");

        (await Fetch()).Should().BeEmpty();
        File.Exists(Lock).Should().BeTrue("a build that declares nothing takes no pin out");
        Directory.GetFiles(Folder).Should().BeEmpty();
    }

    /// <summary>The builds of a Photon app's target frameworks may run at once, each with its own
    /// declarations: they take turns over the lock, and neither writes one without the other's pins.</summary>
    [Fact]
    public async Task TwoBuildsAtOnce_LoseNoPin()
    {
        await Task.WhenAll(
            Fetch(FolderOf("Debug"), Declared("Inter", Cut(400))),
            Fetch(FolderOf("Release"), Declared("JetBrains Mono", Cut(400))));

        var pinned = FontLock.Read(Lock);
        pinned.Of(GoogleFontFetch.Provider, "Inter").Keys.Should().Equal(Cut(400));
        pinned.Of(GoogleFontFetch.Provider, "JetBrains Mono").Keys.Should().Equal(Cut(400));
    }

    /// <summary>Two builds of one project with obj folders of their own share its lock, and take turns over
    /// it: the turn is the lock's, not a store's (Copilot's third review of #1085).</summary>
    [Fact]
    public async Task TwoBuildsWithStoresOfTheirOwn_LoseNoPin()
    {
        await Task.WhenAll(
            GoogleFontFetch.FetchAsync([Declared("Inter", Cut(400))], Lock, Path.Combine(_dir, "obj-a"), Path.Combine(_dir, "obj-a", "fonts"), new GoogleFonts(new HttpClient(_google))),
            GoogleFontFetch.FetchAsync([Declared("JetBrains Mono", Cut(400))], Lock, Path.Combine(_dir, "obj-b"), Path.Combine(_dir, "obj-b", "fonts"), new GoogleFonts(new HttpClient(_google))));

        var pinned = FontLock.Read(Lock);
        pinned.Of(GoogleFontFetch.Provider, "Inter").Keys.Should().Equal(Cut(400));
        pinned.Of(GoogleFontFetch.Provider, "JetBrains Mono").Keys.Should().Equal(Cut(400));
    }

    /// <summary>A project reached through a symbolic link is one project: its lock is named alike either way,
    /// so the builds that reach it by the two paths take turns.</summary>
    [Fact]
    public void ALockReachedThroughALink_IsTheSameLock()
    {
        var link = _dir + "-link";
        try
        {
            Directory.CreateSymbolicLink(link, _dir);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return; // A Windows account without the right to make links: nothing to compare.
        }
        try
        {
            GoogleFontFetch.Canonical(Path.Combine(link, FontLock.FileName)).Should().Be(GoogleFontFetch.Canonical(Lock));
            GoogleFontFetch.Canonical(Path.Combine(link, "obj", "..", FontLock.FileName)).Should().Be(GoogleFontFetch.Canonical(Lock));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    /// <summary>Only a 400 is Google saying it has no such family: a probe answered with 503 is Google failing
    /// to answer, and the build says so rather than that the family does not exist (Copilot's third review
    /// of #1085).</summary>
    [Fact]
    public async Task AProbeGoogleFailsToAnswer_IsNotAFamilyItDoesNotHave()
    {
        _google.ApiAnswersBeforeOutage = 1;

        var errors = await Fetch(Declared("Bebas Neue", Cut(700)));

        errors.Should().ContainSingle().Which.Should()
            .StartWith($"{Place}: error EQ4008: Google Fonts did not answer what it has of 'Bebas Neue'")
            .And.Contain("503");
    }

    [Fact]
    public async Task ThePinsOfAnotherProvider_AreKept()
    {
        var other = new FontLock();
        other.Set("bunny", "Inter", new Dictionary<FontCut, FontLock.Pin> { [Cut(400)] = new(new Uri("https://fonts.bunny.net/inter-400.ttf"), new string('c', 64)) });
        other.Write(Lock);

        (await Fetch(Declared("Inter", Cut(400)))).Should().BeEmpty();

        FontLock.Read(Lock).Of("bunny", "Inter").Should().ContainKey(Cut(400));
    }

    [Fact]
    public async Task ALockThatDoesNotHold_FailsTheBuild_AndIsNeverReadAsFewerPins()
    {
        File.WriteAllText(Lock, "{ \"version\": 1, \"google\": { \"Inter\": { \"400\": { \"url\": \"https://fonts.gstatic.com/x.ttf\" } } } }");

        var errors = await Fetch(Declared("Inter", Cut(400)));

        errors.Should().ContainSingle().Which.Should().StartWith($"{Lock} : error EQ4009: {FontLock.FileName} does not hold: '400' of 'Inter' has no URL and SHA-256 that hold");
        _google.Asked.Should().BeEmpty();
    }

    [Fact]
    public void TheLock_IsWrittenInOrder_SoADiffShowsThePinThatMoved()
    {
        var pins = new FontLock();
        pins.Set("google", "Inter", new Dictionary<FontCut, FontLock.Pin>
        {
            [Cut(700, italic: true)] = new(new Uri("https://fonts.gstatic.com/s/inter/v20/b.ttf"), new string('b', 64)),
            [Cut(400)] = new(new Uri("https://fonts.gstatic.com/s/inter/v20/a.ttf"), new string('a', 64)),
        });

        pins.Text().Should().Be("""
            {
              "version": 1,
              "google": {
                "Inter": {
                  "400": {
                    "url": "https://fonts.gstatic.com/s/inter/v20/a.ttf",
                    "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                  },
                  "700 italic": {
                    "url": "https://fonts.gstatic.com/s/inter/v20/b.ttf",
                    "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                  }
                }
              }
            }

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void TheQuery_AsksEveryCutInTheOrderTheApiRequires()
    {
        GoogleFonts.Query("JetBrains Mono", [Cut(700, italic: true), Cut(400), Cut(700)]).AbsoluteUri
            .Should().Be("https://fonts.googleapis.com/css2?family=JetBrains+Mono:ital,wght@0,400;0,700;1,700");
        GoogleFonts.Query("Inter", [Cut(700), Cut(400)]).AbsoluteUri
            .Should().Be("https://fonts.googleapis.com/css2?family=Inter:wght@400;700");
    }

    private Task<IReadOnlyList<string>> Fetch(params GoogleFontDeclarations.Declaration[] declarations) => Fetch(Folder, declarations);

    private Task<IReadOnlyList<string>> Fetch(string folder, params GoogleFontDeclarations.Declaration[] declarations) =>
        GoogleFontFetch.FetchAsync(declarations, Lock, Store, folder, new GoogleFonts(new HttpClient(_google)));

    /// <summary>Takes the file a pin names out of the store, as a clone with no obj would not have it.</summary>
    private void ForgetStored(string family, FontCut cut) =>
        File.Delete(Path.Combine(Store, $"{FontLock.Read(Lock).Of(GoogleFontFetch.Provider, family)[cut].Sha256}.ttf"));

    /// <summary>Every file of <paramref name="folder"/>, by name, with its bytes.</summary>
    private static Dictionary<string, string> Contents(string folder) =>
        Directory.GetFiles(folder).ToDictionary(file => Path.GetFileName(file), file => Sha256(File.ReadAllBytes(file)));

    private static GoogleFontDeclarations.Declaration Declared(string family, params FontCut[] cuts) =>
        new(family, [.. cuts.Order()], Place);

    private static FontCut Cut(int weight, bool italic = false) => new(weight, italic);

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// Google as the tests see it: families with the cuts they have, at a version, each file a font built
    /// for its family, weight, italic and version, and every request it was asked. Its API answers in the
    /// format the real one was measured to answer a client that is not a browser, and with 400 and the
    /// same sentence for a family, a weight or an italic it does not have.
    /// </summary>
    private sealed class FakeGoogle : HttpMessageHandler
    {
        private readonly Dictionary<string, (int Version, FontCut[] Cuts)> _families = new()
        {
            ["Inter"] = (1, [new(400, false), new(700, false), new(400, true), new(700, true)]),
            ["JetBrains Mono"] = (1, [new(400, false), new(400, true)]),
            ["Bebas Neue"] = (1, [new(400, false)]),
        };

        public List<Uri> Asked { get; } = [];

        /// <summary>Files that changed in place, at the URL they always had.</summary>
        public Dictionary<Uri, byte[]> Changed { get; } = [];

        /// <summary>URLs Google no longer serves.</summary>
        public HashSet<Uri> Gone { get; } = [];

        public bool Unreachable { get; set; }

        /// <summary>How many requests the API answers before it answers 503 to every one, or null for never.</summary>
        public int? ApiAnswersBeforeOutage { get; set; }

        private int _apiAnswered;

        public static bool IsApi(Uri uri) => uri.Host == "fonts.googleapis.com";

        /// <summary>Google publishes a new version of <paramref name="family"/>, at new URLs, the old ones still served.</summary>
        public void Replace(string family) => _families[family] = (_families[family].Version + 1, _families[family].Cuts);

        /// <summary>Google stops offering <paramref name="cut"/> of <paramref name="family"/>; its files stay served.</summary>
        public void Drop(string family, FontCut cut) => _families[family] = (_families[family].Version, _families[family].Cuts.Where(kept => kept != cut).ToArray());

        public Uri UrlOf(string family, FontCut cut, int? version = null) =>
            new($"https://fonts.gstatic.com/s/{Slug(family)}/v{version ?? _families[family].Version}/{cut.Weight}{(cut.Italic ? "i" : "")}.ttf");

        public byte[] FileOf(string family, FontCut cut, int? version = null) =>
            TestFont.Build(family, cut.Weight, cut.Italic, advance: 1000 + (version ?? _families[family].Version));

        /// <summary>Answers after a real wait, as the network does, so two builds that ask at once interleave.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            lock (Asked) Asked.Add(uri);
            await Task.Delay(5, cancellationToken);
            if (Unreachable) throw new HttpRequestException("No such host is known.");
            if (IsApi(uri) && Interlocked.Increment(ref _apiAnswered) > ApiAnswersBeforeOutage)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return IsApi(uri) ? Css(uri) : File(uri);
        }

        private HttpResponseMessage Css(Uri uri)
        {
            var query = Uri.UnescapeDataString(uri.Query.TrimStart('?')["family=".Length..].Replace('+', ' '));
            var family = query.Split(':')[0];
            if (!_families.TryGetValue(family, out var known)) return NotAvailable();
            var asked = query.Contains(':') ? Cuts(query.Split(':')[1]) : [known.Cuts[0]];
            if (asked.Any(cut => !known.Cuts.Contains(cut))) return NotAvailable();
            var css = new StringBuilder();
            foreach (var cut in asked.OrderByDescending(cut => cut.Italic).ThenBy(cut => cut.Weight))
                css.Append($"@font-face {{\n  font-family: '{family}';\n  font-style: {(cut.Italic ? "italic" : "normal")};\n"
                    + $"  font-weight: {cut.Weight};\n  src: url({UrlOf(family, cut)}) format('truetype');\n}}\n");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(css.ToString(), Encoding.UTF8, "text/css") };
        }

        private HttpResponseMessage File(Uri uri)
        {
            if (Gone.Contains(uri)) return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Changed.TryGetValue(uri, out var changed)) return Bytes(changed);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var family = _families.Keys.SingleOrDefault(name => Slug(name) == segments[1]);
            if (family is null || !int.TryParse(segments[2].TrimStart('v'), out var version) || version > _families[family].Version)
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            var name = Path.GetFileNameWithoutExtension(segments[3]);
            var cut = new FontCut(int.Parse(name.TrimEnd('i')), name.EndsWith('i'));
            return Bytes(FileOf(family, cut, version));
        }

        private static FontCut[] Cuts(string axes)
        {
            var (names, tuples) = (axes.Split('@')[0].Split(','), axes.Split('@')[1].Split(';'));
            return tuples.Select(tuple => tuple.Split(','))
                .Select(values => names.Length == 2
                    ? new FontCut(int.Parse(values[1]), values[0] == "1")
                    : new FontCut(int.Parse(values[0]), false))
                .ToArray();
        }

        private static HttpResponseMessage NotAvailable() =>
            new(HttpStatusCode.BadRequest) { Content = new StringContent("<p>The requested font families are not available.</p>") };

        private static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

        private static string Slug(string family) => family.Replace(" ", "").ToLowerInvariant();
    }
}
