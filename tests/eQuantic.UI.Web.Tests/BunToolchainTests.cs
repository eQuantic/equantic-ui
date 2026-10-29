using System.Security.Cryptography;
using System.Text.Json;
using eQuantic.UI.Sdk.Tasks;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The embedded Bun is PINNED: one version, one digest per platform, both checked.
/// <para>
/// Bun is committed rather than downloaded on demand — every app build needs it, so it ships inside
/// the platform runtime packages a consumer restores (see scripts/slang-toolchain.sh, which
/// explains why slangc took the other road). What it lacked was the thing a pin buys: the version
/// could only be learned by unzipping a binary and running it, and nothing proved the bytes were
/// the ones the release published. Asking "is the embedded Bun current?" meant archaeology.
/// </para>
/// </summary>
public class BunToolchainTests
{
    private sealed record Manifest(string Version, Dictionary<string, string> Artifacts);

    private static string RepoRoot()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "src", "eQuantic.UI.Runtime")))
            here = here.Parent;
        here.Should().NotBeNull();
        return here!.FullName;
    }

    private static string ManifestPath(string root) =>
        Path.Combine(root, "src", "eQuantic.UI.Runtime", "bun-toolchain.json");

    private static Manifest Read(string root)
    {
        var json = JsonDocument.Parse(File.ReadAllText(ManifestPath(root))).RootElement;
        return new Manifest(
            json.GetProperty("version").GetString()!,
            json.GetProperty("artifacts").EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString()!));
    }

    private static string Digest(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>
    /// <paramref name="json"/> with <paramref name="relative"/>'s digest replaced by <paramref name="digest"/>.
    /// The group is written <c>${1}</c>, never <c>$1</c>: a digest that starts with a digit read as part
    /// of the group's number, <c>$17b3…</c> for a group 17 that is not there, and the line lost its key.
    /// </summary>
    private static string WithDigest(string json, string relative, string digest) =>
        System.Text.RegularExpressions.Regex.Replace(
            json, $"(\"{System.Text.RegularExpressions.Regex.Escape(relative)}\": \")[a-f0-9]+", $"${{1}}{digest}");

    /// <summary>
    /// The regeneration runs once an update, and the pin's first was the move to 1.4.3: it wrote four
    /// of six lines without their key, the four whose digest starts with a digit (#854).
    /// </summary>
    [Theory]
    [InlineData("7b3ab8150d7a615b")]
    [InlineData("c34f35f31d0de594")]
    public void ARegeneratedDigest_KeepsItsLine_WhateverItStartsWith(string digest)
    {
        const string line = "    \"eQuantic.UI.Runtime.Linux64/tools/bun/bun-linux.zip\": \"79db9fbe0dc4558d\",";

        WithDigest(line, "eQuantic.UI.Runtime.Linux64/tools/bun/bun-linux.zip", digest)
            .Should().Be($"    \"eQuantic.UI.Runtime.Linux64/tools/bun/bun-linux.zip\": \"{digest}\",");
    }

    [Fact]
    public void EveryCommittedBun_MatchesItsPinnedDigest()
    {
        var root = RepoRoot();
        var manifest = Read(root);

        // Regenerating is deliberate and explicit: updating Bun means new bytes, and the diff is
        // where a reviewer sees a version move beside a digest move.
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_BUN_MANIFEST") == "1")
        {
            var refreshed = manifest.Artifacts.Keys.ToDictionary(
                relative => relative,
                relative => Digest(Path.Combine(root, "src", relative)));
            var json = File.ReadAllText(ManifestPath(root));
            foreach (var (relative, digest) in refreshed) json = WithDigest(json, relative, digest);
            File.WriteAllText(ManifestPath(root), json);
            return;
        }

        var wrong = new List<string>();
        foreach (var (relative, expected) in manifest.Artifacts)
        {
            var path = Path.Combine(root, "src", relative);
            if (!File.Exists(path)) { wrong.Add($"{relative}: missing"); continue; }
            var actual = Digest(path);
            if (actual != expected) wrong.Add($"{relative}: {actual}");
        }

        string.Join("\n", wrong).Should().BeEmpty(
            "a build tool that ships to every consumer is the last place to accept bytes nobody "
            + "checked — regenerate with EQ_UPDATE_BUN_MANIFEST=1 when the update is intentional");
    }

    /// <summary>
    /// And the pinned VERSION is the one the binary reports. A digest proves the bytes did not
    /// change; only running it proves what they are — which is the question anyone actually asks
    /// ("is the embedded Bun current?") and the one that used to need a manual unzip to answer.
    /// </summary>
    [Fact]
    public void ThePinnedVersion_IsWhatTheBinaryReports()
    {
        var root = RepoRoot();
        var manifest = Read(root);

        // The host's own artifact: the only one this machine can execute.
        var relative = OperatingSystem.IsMacOS()
            ? "eQuantic.UI.Runtime.OsxArm64/tools/bun/bun-darwin.zip"
            : OperatingSystem.IsLinux()
                ? "eQuantic.UI.Runtime.Linux64/tools/bun/bun-linux.zip"
                : "eQuantic.UI.Runtime.Win64/tools/bun/bun.exe.zip";
        var archive = Path.Combine(root, "src", relative);
        if (!File.Exists(archive)) return;

        // Unpacked by the owner every build runs, ProgramArchive, so the zip that is pinned is also
        // proven to hold the program it is named for, ready to run (#364).
        var scratch = Directory.CreateTempSubdirectory("eq-bun");
        try
        {
            var (binary, _) = ProgramArchive.Unpack(archive, scratch.FullName);

            // A minute for a program that answers in milliseconds: a Bun that hangs fails this case
            // rather than holding the test host (#971).
            var reported = ChildProcess.Run(binary, ["--version"], TimeSpan.FromMinutes(1)).Output.Trim();

            reported.Should().Be(manifest.Version,
                "the manifest is the answer to \"which Bun is embedded\", and an answer nobody "
                + "checks is how it drifts from the binary beside it");
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }
}
