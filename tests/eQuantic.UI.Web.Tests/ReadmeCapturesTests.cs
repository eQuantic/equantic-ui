using System.Security.Cryptography;
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
/// </summary>
public class ReadmeCapturesTests
{
    private const string Record = "assets/readme/captures.json";
    private const string Folder = "assets/readme";

    /// <summary>Where a picture is read from. The README is every package's readme on nuget.org
    /// too, which draws a Markdown image from this host and nothing from a relative path.</summary>
    private const string Address = "https://raw.githubusercontent.com/eQuantic/equantic-ui/main/assets/readme/";

    /// <summary>A floor against the vacuous pass: a README that lost its pictures and a record that
    /// lost its entries would agree with each other.</summary>
    private const int FewestPictures = 6;

    [Fact]
    public void EveryPictureTheReadmeShows_IsACaptureOnRecord_AndEveryCaptureIsShown()
    {
        var root = Root();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        var shown = Regex.Matches(readme, Regex.Escape(Address) + @"(?<name>[A-Za-z0-9._-]+)")
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
            $"README.md shows its samples through pictures under {Address}");
        recorded.Should().Equal(shown,
            $"{Record} holds one entry for each picture README.md shows, and none it does not show");
        files.Should().Equal(recorded, $"{Folder} holds the pictures on record and no other file");
    }

    /// <summary>
    /// nuget.org shows a readme's HTML as text. Measured on the page of 0.2.0-preview.61: the
    /// README's own <c>&lt;img src="https://img.shields.io/…"&gt;</c> badges are there as
    /// <c>&amp;lt;img src</c>, drawn by nothing. A Markdown image from this host is drawn.
    /// </summary>
    [Fact]
    public void APictureIsAMarkdownImage_NamedByItsFullAddress()
    {
        var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));

        Regex.Matches(readme, @"(?:src=[""']|\]\()(?<path>\.?/?assets/readme/[^""'\)\s]+)")
            .Select(match => match.Groups["path"].Value)
            .Should().BeEmpty(
                "README.md is also the readme of every package on nuget.org, which draws nothing from a "
                + $"relative path: a picture is named by {Address}<file>");

        Regex.Matches(readme, @"<img[^>]*" + Regex.Escape(Address) + @"(?<name>[A-Za-z0-9._-]+)")
            .Select(match => match.Groups["name"].Value)
            .Should().BeEmpty(
                "nuget.org shows a readme's HTML as text, so an <img> draws nothing there: a picture is "
                + $"written ![what it shows]({Address}<file>)");
    }

    [Fact]
    public void APictureIsTakenAgain_WhenAFileItsScreenIsWrittenInChanges()
    {
        var root = Root();
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

    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull("the suite runs inside the repository, whose root holds src/eQuantic.UI.Runtime");
        return here!.FullName;
    }
}
