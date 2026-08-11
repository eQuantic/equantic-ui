using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
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
            foreach (var (relative, digest) in refreshed)
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, $"(\"{System.Text.RegularExpressions.Regex.Escape(relative)}\": \")[a-f0-9]+", $"$1{digest}");
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

        var scratch = Directory.CreateTempSubdirectory("eq-bun");
        try
        {
            ZipFile.ExtractToDirectory(archive, scratch.FullName);
            var binary = Directory.GetFiles(scratch.FullName, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith("bun", StringComparison.Ordinal));
            binary.Should().NotBeNull($"{relative} should contain a bun binary");

            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(binary!, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(binary!, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            var reported = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

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
