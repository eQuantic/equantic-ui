using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using eQuantic.UI.Sdk.Tasks;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The embedded bun is unpacked by one owner, <see cref="ProgramArchive"/>, into the user's cache under
/// the SHA-256 of its zip. It was unpacked beside the zip, and only when no binary was there: into the
/// NuGet package cache, which may be read-only (#364), and in a source tree, where a worktree ran the
/// bun of the first zip it ever saw after the zip moved to another version (#932).
/// </summary>
public class ProgramArchiveTests
{
    /// <summary>A program that says <paramref name="word"/>: a script on macOS and Linux, text on Windows.</summary>
    private static string Zip(string folder, string name, string word)
    {
        Directory.CreateDirectory(folder);
        var archive = Path.Combine(folder, name + ".zip");
        if (File.Exists(archive)) File.Delete(archive);
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write($"#!/bin/sh\necho {word}\n");
        return archive;
    }

    private static string Says(string program) =>
        OperatingSystem.IsWindows()
            ? File.ReadAllText(program)
            : ChildProcess.Run(program, [], TimeSpan.FromMinutes(1)).Output.Trim();

    private static string Digest(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    [Fact]
    public void AZip_IsUnpackedUnderItsContent_AndAZipThatMovesOn_IsUnpackedAgain()
    {
        var root = Directory.CreateTempSubdirectory("eq-unpack-").FullName;
        try
        {
            var cache = Path.Combine(root, "cache");
            var archive = Zip(Path.Combine(root, "tools $b"), "tool", "first");

            var (program, unpacked) = ProgramArchive.Unpack(archive, cache);

            unpacked.Should().BeTrue();
            program.Should().Be(Path.Combine(cache, "tool", Digest(archive), "tool"));
            Says(program).Should().Contain("first");
            Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(archive)!).Should().Equal([archive],
                "nothing is written beside the zip");

            ProgramArchive.Unpack(archive, cache).Should().Be((program, false), "the same zip is unpacked once");

            // The tracked zip moves to another version, in the same place: the program that runs is the
            // new zip's. Unpacked beside it, the first program stayed and went on running (#932).
            Zip(Path.GetDirectoryName(archive)!, "tool", "second");
            var (moved, again) = ProgramArchive.Unpack(archive, cache);

            again.Should().BeTrue();
            moved.Should().NotBe(program);
            Says(moved).Should().Contain("second");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [UnixFact]
    public void AZipInAFolderNobodyMayWrite_IsUnpacked()
    {
        // A package cache restored read-only, baked into an image or shared on a volume (#364).
        if (OperatingSystem.IsWindows()) return;
        var root = Directory.CreateTempSubdirectory("eq-unpack-").FullName;
        var packages = Path.Combine(root, "packages");
        try
        {
            var archive = Zip(packages, "tool", "read-only");
            File.SetUnixFileMode(packages, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var (program, _) = ProgramArchive.Unpack(archive, Path.Combine(root, "cache"));

            Says(program).Should().Contain("read-only");
            File.GetUnixFileMode(program).Should().HaveFlag(UnixFileMode.UserExecute);
        }
        finally
        {
            File.SetUnixFileMode(packages, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildsUnpackingOneZipAtOnce_LeaveOneWholeFolder()
    {
        // Two builds starting together on a clean cache unpacked into one folder (#364).
        var root = Directory.CreateTempSubdirectory("eq-unpack-").FullName;
        try
        {
            var cache = Path.Combine(root, "cache");
            var archive = Zip(Path.Combine(root, "tools"), "tool", "shared");

            var results = new (string Program, bool Unpacked)[8];
            Parallel.For(0, results.Length, i => results[i] = ProgramArchive.Unpack(archive, cache));

            results.Select(result => result.Program).Distinct().Should().ContainSingle();
            results.Count(result => result.Unpacked).Should().Be(1, "one build unpacks, and the others find its folder");
            Directory.EnumerateFileSystemEntries(Path.Combine(cache, "tool")).Should().ContainSingle(
                "no half-unpacked folder is left behind");
            Says(results[0].Program).Should().Contain("shared");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AFolderThatLostItsProgram_IsUnpackedAgain()
    {
        var root = Directory.CreateTempSubdirectory("eq-unpack-").FullName;
        try
        {
            var cache = Path.Combine(root, "cache");
            var archive = Zip(Path.Combine(root, "tools"), "tool", "back");
            var (program, _) = ProgramArchive.Unpack(archive, cache);
            File.Delete(program);

            ProgramArchive.Unpack(archive, cache).Should().Be((program, true));
            Says(program).Should().Contain("back");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AZipWithoutItsProgram_IsRefused_AndLeavesNothing()
    {
        var root = Directory.CreateTempSubdirectory("eq-unpack-").FullName;
        try
        {
            var cache = Path.Combine(root, "cache");
            var archive = Zip(Path.Combine(root, "tools"), "other", "x");
            var renamed = Path.Combine(root, "tools", "tool.zip");
            File.Move(archive, renamed);

            var unpack = () => ProgramArchive.Unpack(renamed, cache);

            unpack.Should().Throw<InvalidDataException>().WithMessage("*holds no entry named tool*");
            Directory.EnumerateFileSystemEntries(Path.Combine(cache, "tool")).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [UnixFact]
    public void ACacheThatCannotBeWritten_FailsTheBuild_NamingTheZip()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Directory.CreateTempSubdirectory("eq-unpack-").FullName;
        var cache = Directory.CreateDirectory(Path.Combine(root, "cache")).FullName;
        try
        {
            var archive = Zip(Path.Combine(root, "tools"), "tool", "x");
            File.SetUnixFileMode(cache, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var engine = new RecordingBuildEngine();

            new UnpackProgram { BuildEngine = engine, Archive = archive, Cache = cache }.Execute().Should().BeFalse();

            engine.Errors.Should().ContainSingle().Which.Message.Should().Contain(archive).And.Contain("could not be unpacked");
        }
        finally
        {
            File.SetUnixFileMode(cache, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TheUserCache_IsTheOneTheSystemNames()
    {
        var cache = ProgramArchive.DefaultCache();

        if (OperatingSystem.IsWindows())
            cache.Should().StartWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        else if (OperatingSystem.IsMacOS())
            cache.Should().Be(Path.Combine(Environment.GetEnvironmentVariable("HOME")!, "Library", "Caches", "eQuantic"));
        else
            cache.Should().EndWith(Path.Combine("", "eQuantic")).And.NotContain(".nuget");
    }

    /// <summary>
    /// Every place that runs the bun asks <see cref="ProgramArchive"/>: the SDK's targets, the Server's
    /// and the Runtime's (through the shared EmbeddedBun.targets) and the conformance harness. Each one
    /// unpacked it beside the zip by itself, and only when no binary was there, which is how one
    /// worktree's build and harness ran a bun two versions behind its zip (#932). An unzip anywhere
    /// else in the build files or the harness is one of them coming back.
    /// </summary>
    [Fact]
    public void NothingElseUnpacksTheBun()
    {
        var root = SdkProbe.RepoRoot();
        var buildFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".csproj") || file.EndsWith(".props") || file.EndsWith(".targets"))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        buildFiles.Where(file => File.ReadAllText(file).Contains("<Unzip")).Should().BeEmpty(
            "MSBuild's Unzip unpacks beside the zip, where a package cache may be read-only");

        var harness = File.ReadAllText(Path.Combine(root, "tests", "eQuantic.UI.Conformance.Tests", "Infrastructure", "JsExecutor.cs"));
        Regex.IsMatch(harness, @"ExtractTo(Directory|File)").Should().BeFalse("the harness runs the bun ProgramArchive unpacks");
        harness.Should().Contain("ProgramArchive.Unpack(");

        foreach (var project in new[] { "eQuantic.UI.Server/eQuantic.UI.Server.csproj", "eQuantic.UI.Runtime/eQuantic.UI.Runtime.csproj" })
            File.ReadAllText(Path.Combine(root, "src", project)).Should().Contain("../Shared/EmbeddedBun.targets");
        File.ReadAllText(Path.Combine(root, "src", "eQuantic.UI.Sdk", "Sdk", "Sdk.targets")).Should().Contain("<UnpackProgram ");
    }
}
