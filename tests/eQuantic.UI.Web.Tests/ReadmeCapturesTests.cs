using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The pictures the root <c>README.md</c> shows are captures of the samples, and a capture is taken
/// again when the screen it shows changes (#880).
/// <para>
/// A picture goes stale in silence: the sample's screen changes, the build stays green, and the
/// README goes on showing the screen as it was. So <c>assets/readme/captures.json</c> names, for each
/// picture, the files its screen is written in, with the digest each had when the picture was taken.
/// A file that changed fails here, naming the pictures it belongs to and the target of
/// <c>scripts/capture-readme.sh</c> that takes them again.
/// </para>
/// <para>
/// <c>EQ_UPDATE_README_CAPTURES=1</c> records the files as they are now. It is run AFTER the pictures
/// were taken again and looked at: recording alone makes the guard pass over a picture that still
/// shows the old screen, which is the failure this exists to stop.
/// </para>
/// <para>
/// What it does not see is a change to a component the screen places, since that is no file of the
/// sample: the digests hold the sample's own screens, and a redrawn button reaches the pictures the
/// next time one of them is taken.
/// </para>
/// <para>
/// A picture is read in two places, and each draws one form. GitHub draws a path in the repository
/// on every branch, in a fork and in the pull request that takes the picture again, and it drew
/// nothing on this change's own branch while the README named the pictures by their address on
/// <c>main</c>. nuget.org, where the README is every package's readme, draws nothing from a path:
/// it needs the full address. So README.md names a picture by its path, and a package's readme is
/// written when it packs, with each picture at the address of the release's own tag.
/// </para>
/// </summary>
public class ReadmeCapturesTests
{
    private const string Record = "assets/readme/captures.json";
    private const string Folder = "assets/readme";

    /// <summary>The one way README.md names a picture: a Markdown image that says what it shows, by
    /// the picture's path in the repository.</summary>
    private static readonly Regex Image = new(@"!\[[^\]\r\n]+\]\(assets/readme/(?<name>[A-Za-z0-9._-]+)\)");

    /// <summary>A floor against the vacuous pass: a README that lost its pictures and a record that
    /// lost its entries would agree with each other.</summary>
    private const int FewestPictures = 6;

    [Fact]
    public void EveryPictureTheReadmeShows_IsACaptureOnRecord_AndEveryCaptureIsShown()
    {
        var root = SdkProbe.RepoRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        var shown = Image.Matches(readme)
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        var recorded = Captures(root).Select(capture => capture.Picture).OrderBy(name => name, StringComparer.Ordinal).ToList();
        // A hidden file is the system's, not the repository's: a Mac writes .DS_Store into a folder
        // somebody opened, and git ignores it.
        var files = Directory.GetFiles(Path.Combine(root, Folder))
            .Select(path => Path.GetFileName(path))
            .Where(name => name != Path.GetFileName(Record) && !name.StartsWith('.'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        shown.Count.Should().BeGreaterThanOrEqualTo(FewestPictures,
            $"README.md shows its samples through the pictures in {Folder}");
        recorded.Should().Equal(shown,
            $"{Record} holds one entry for each picture README.md shows, and none it does not show");
        files.Should().Equal(recorded, $"{Folder} holds the pictures on record and no other file");
    }

    /// <summary>
    /// Anything else that names the folder is a picture one of the two readers does not draw. A
    /// full address is tied to one branch. nuget.org shows a readme's HTML as text: on the page of
    /// 0.2.0-preview.61 the README's own <c>&lt;img src="https://img.shields.io/…"&gt;</c> badges
    /// are there as <c>&amp;lt;img src</c>. And a path written any other way is one the pack does
    /// not turn into an address.
    /// </summary>
    [Fact]
    public void TheReadmeNamesAPicture_OnlyAsAMarkdownImage_ByItsPathInTheRepository()
    {
        var readme = File.ReadAllText(Path.Combine(SdkProbe.RepoRoot(), "README.md"));

        Regex.Matches(Image.Replace(readme, string.Empty), @"\S*assets/readme\S*")
            .Select(match => match.Value)
            .Should().BeEmpty(
                "README.md names a picture one way, ![what it shows](assets/readme/<file>): GitHub draws "
                + "that path on every branch and in a fork, and a package's readme gets the full address "
                + "when it packs. A link, an <img>, a full address or a path written another way is not "
                + "drawn by one of the two");
    }

    /// <summary>
    /// The target every package packs its readme through, run as a pack runs it. What it writes is
    /// README.md byte for byte, but for each picture's path, which is the address of the picture at
    /// the release's tag.
    /// <para>
    /// The comparison is the point. The text was first handed to MSBuild's own tasks, and on macOS
    /// the README's one backslash was packed as a slash, with every picture's address right.
    /// </para>
    /// </summary>
    [Fact]
    public void APackagesReadme_IsTheReadme_WithEachPictureAtItsReleasesAddress()
    {
        var root = SdkProbe.RepoRoot();
        var scratch = Directory.CreateTempSubdirectory("eq-readme-").FullName;
        try
        {
            var written = Path.Combine(scratch, "README.md");
            SdkProbe.Dotnet(null, null, "msbuild",
                Path.Combine("src", "eQuantic.UI.Primitives", "eQuantic.UI.Primitives.csproj"),
                "-nologo", "-nr:false", "-v:m", "-t:WritePackageReadme",
                "-p:Version=9.9.9-probe", $"-p:EqPackageReadme={written}");

            var expected = File.ReadAllText(Path.Combine(root, "README.md")).Replace(
                "](assets/readme/",
                "](https://raw.githubusercontent.com/eQuantic/equantic-ui/v9.9.9-probe/assets/readme/");

            File.ReadAllText(written).Should().Be(expected,
                "a package's readme is README.md with each picture at the address of its release's tag");
            File.ReadAllBytes(written).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected)).Should().BeTrue(
                "the package's readme holds the README's own bytes, with no mark in front of them");
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    [Fact]
    public void APictureIsTakenAgain_WhenAFileItsScreenIsWrittenInChanges()
    {
        var root = SdkProbe.RepoRoot();
        var captures = Captures(root);

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_README_CAPTURES") == "1")
        {
            Write(root, captures);
            return;
        }

        var changed = new List<string>();
        foreach (var capture in captures)
        {
            capture.Sources.Should().NotBeEmpty($"{capture.Picture} names the files its screen is written in");
            foreach (var (source, digest) in capture.Sources)
            {
                var path = Path.Combine(root, source);
                if (!File.Exists(path))
                    changed.Add($"{capture.Picture} ({capture.Target}): {source} is gone");
                else if (Digest(path) != digest)
                    changed.Add($"{capture.Picture} ({capture.Target}): {source} changed");
            }
        }

        changed.Should().BeEmpty(
            "a picture in README.md shows a sample's screen as its files were when it was taken. Take the "
            + "picture again with ./scripts/capture-readme.sh <target> compose, look at it, then record the "
            + "files as they are with EQ_UPDATE_README_CAPTURES=1 on this test");
    }

    private sealed record Capture(string Picture, string Target, IReadOnlyList<(string Source, string Digest)> Sources);

    private static List<Capture> Captures(string root)
    {
        var path = Path.Combine(root, Record);
        File.Exists(path).Should().BeTrue($"the README's pictures are on record at {Record}");
        var captures = JsonNode.Parse(File.ReadAllText(path))!["captures"]!.AsObject();
        return captures
            .Select(entry => new Capture(
                entry.Key,
                entry.Value!["target"]!.GetValue<string>(),
                entry.Value["sources"]!.AsObject()
                    .Select(source => (source.Key, source.Value!.GetValue<string>()))
                    .ToList()))
            .ToList();
    }

    /// <summary>The record again, with each file's digest as it is now and everything else as it
    /// was: the comment, the order, and which files belong to which picture.</summary>
    private static void Write(string root, IReadOnlyList<Capture> captures)
    {
        var path = Path.Combine(root, Record);
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        foreach (var capture in captures)
        {
            var sources = document["captures"]![capture.Picture]!["sources"]!.AsObject();
            foreach (var (source, _) in capture.Sources)
                sources[source] = Digest(Path.Combine(root, source));
        }

        File.WriteAllText(path, FixtureJson.Write(document, FixtureJson.Readable));
    }

    private static string Digest(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
