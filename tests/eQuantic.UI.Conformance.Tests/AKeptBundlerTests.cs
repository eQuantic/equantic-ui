using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Tests;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A bundler kept between bundles, as a served eqc keeps one between saves (#1280), through the REAL
/// bun: it bundles every entry point every time, so the chunks they share stay valid, and writes what
/// changed and nothing else, so a save that touches one page rewrites that page.
/// </summary>
public class AKeptBundlerTests
{
    [SkippableFact]
    public void OneBundler_StartsBunOnce_ForEveryBundleItIsHanded()
    {
        Skip.If(OperatingSystem.IsWindows(), "the stand-in that counts bun's starts is a shell script");
        Bundling((bun, ts, output) =>
        {
            // A stand-in for bun that writes a line each time it starts, then is bun.
            var starts = Path.Combine(Path.GetDirectoryName(output)!, "starts.log");
            var standIn = Path.Combine(Path.GetDirectoryName(output)!, "bun-stand-in");
            File.WriteAllText(standIn, $"#!/bin/sh\necho started >> '{starts}'\nexec '{bun}' \"$@\"\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(standIn, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var entries = Pages(ts);

            using (var bundler = new ModuleBundler(standIn, ts))
            {
                bundler.Bundle(entries, output, ts, SourceMapMode.None).Should().BeNull();
                File.WriteAllText(Path.Combine(ts, "Second.ts"), "import { shared } from './Shared';\nexport const second = shared + 3;\n");
                bundler.Bundle(entries, output, ts, SourceMapMode.None).Should().BeNull();
                bundler.Bundle(entries, output, ts, SourceMapMode.None).Should().BeNull();
            }

            File.ReadAllLines(starts).Should().HaveCount(1, "a bundler hands every bundle to the bun it started first");
        });
    }

    [SkippableFact]
    public void ASecondBundle_RewritesNothingThatDidNotChange()
    {
        Bundling((bun, ts, output) =>
        {
            var entries = Pages(ts);
            using var bundler = new ModuleBundler(bun, ts);
            bundler.Bundle(entries, output, ts, SourceMapMode.Full).Should().BeNull();
            var first = Aged(output);
            first.Keys.Should().Contain(file => file.EndsWith(".js.map", StringComparison.Ordinal), "a Debug bundle writes maps");
            first.Keys.Should().Contain(file => Path.GetFileName(file).Contains('-'), "the two pages share a chunk");

            bundler.Bundle(entries, output, ts, SourceMapMode.Full).Should().BeNull();

            Snapshot(output).Should().Equal(first, "a bundle of what did not change writes nothing and removes nothing");
        });
    }

    [SkippableFact]
    public void AChangedModule_IsWrittenAgain_AndNothingElse()
    {
        Bundling((bun, ts, output) =>
        {
            var entries = Pages(ts);
            using var bundler = new ModuleBundler(bun, ts);
            bundler.Bundle(entries, output, ts, SourceMapMode.None).Should().BeNull();
            var first = Aged(output);

            File.WriteAllText(Path.Combine(ts, "Second.ts"), "import { shared } from './Shared';\nexport const second = shared + 20;\n");
            bundler.Bundle(entries, output, ts, SourceMapMode.None).Should().BeNull();

            var second = Snapshot(output);
            second[Path.Combine(output, "Second.js")].Should().NotBe(first[Path.Combine(output, "Second.js")], "its text changed");
            second.Where(pair => !pair.Key.EndsWith("Second.js", StringComparison.Ordinal))
                .Should().Equal(first.Where(pair => !pair.Key.EndsWith("Second.js", StringComparison.Ordinal)),
                    "the other page and the chunk they share are what they were");
        });
    }

    [SkippableFact]
    public void AMapIsComposedAgain_WhenTheMapItComposesWithChanged_ThoughBunWroteTheSame()
    {
        Bundling((bun, ts, output) =>
        {
            // The TypeScript of a C# file whose lines moved is the same text with another map: bun's own
            // map for it does not change, and the composed one has to.
            var entry = Path.Combine(ts, "Page.ts");
            File.WriteAllText(entry, "export const page = 1;\n//# sourceMappingURL=Page.ts.map\n");
            File.WriteAllText(entry + ".map", TypeScriptMap("../Pages/Page.cs"));
            using var bundler = new ModuleBundler(bun, ts);
            bundler.Bundle([entry], output, ts, SourceMapMode.Full).Should().BeNull();
            File.ReadAllText(Path.Combine(output, "Page.js.map")).Should().Contain("Page.cs");

            File.WriteAllText(entry + ".map", TypeScriptMap("../Pages/Moved.cs"));
            File.SetLastWriteTimeUtc(entry + ".map", DateTime.UtcNow.AddMinutes(1));
            bundler.Bundle([entry], output, ts, SourceMapMode.Full).Should().BeNull();

            File.ReadAllText(Path.Combine(output, "Page.js.map")).Should().Contain("Moved.cs")
                .And.NotContain("Pages/Page.cs", "the module's map leads to where its C# is now");
        });
    }

    [SkippableFact]
    public void AReleaseBundle_IsTheBytesBunsCommandLineWrites()
    {
        Bundling((bun, ts, output) =>
        {
            // What a publish copies behaves as before: the bundle of a build with no maps is the bytes
            // `bun build` wrote with the flags eqc handed it until #1280.
            var entries = Pages(ts);
            var commandLine = Path.Combine(Path.GetDirectoryName(output)!, "cli");
            var (code, _, error) = ChildProcess.Run(bun,
                ["build", .. entries, "--outdir", commandLine, "--root", ts, "--splitting", "--minify-syntax",
                 "--minify-whitespace", "--target", "browser", "--external", "@equantic/runtime"],
                TimeSpan.FromMinutes(1), ts);
            code.Should().Be(0, error);

            using (var bundler = new ModuleBundler(bun, ts)) bundler.Bundle(entries, output, ts, SourceMapMode.None).Should().BeNull();

            Directory.GetFiles(output).Select(Path.GetFileName).Should().BeEquivalentTo(
                Directory.GetFiles(commandLine).Select(Path.GetFileName));
            foreach (var file in Directory.GetFiles(commandLine))
                File.ReadAllBytes(Path.Combine(output, Path.GetFileName(file))).Should()
                    .Equal(File.ReadAllBytes(file), $"{Path.GetFileName(file)} is the bytes the command line wrote");
        });
    }

    /// <summary>Two pages that share a module, so the bundle has a chunk.</summary>
    private static List<string> Pages(string ts)
    {
        File.WriteAllText(Path.Combine(ts, "Shared.ts"), "export const shared = 41;\n");
        File.WriteAllText(Path.Combine(ts, "First.ts"), "import { shared } from './Shared';\nexport const first = shared + 1;\n");
        File.WriteAllText(Path.Combine(ts, "Second.ts"), "import { shared } from './Shared';\nexport const second = shared + 2;\n");
        return [Path.Combine(ts, "First.ts"), Path.Combine(ts, "Second.ts")];
    }

    private static string TypeScriptMap(string source) =>
        $$"""{"version":3,"sources":["{{source}}"],"sourcesContent":[null],"mappings":"AAAA","names":[]}""";

    /// <summary>Every file of the folder made an hour old, so a file written again shows by its time.</summary>
    private static Dictionary<string, string> Aged(string output)
    {
        foreach (var file in Directory.GetFiles(output)) File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-1));
        return Snapshot(output);
    }

    /// <summary>Every file of the folder by its time and its text.</summary>
    private static Dictionary<string, string> Snapshot(string output) =>
        Directory.GetFiles(output).Order(StringComparer.Ordinal)
            .ToDictionary(file => file, file => $"{File.GetLastWriteTimeUtc(file):O}|{File.ReadAllText(file)}");

    private static void Bundling(Action<string, string, string> test)
    {
        var bun = JsExecutor.RequireBun();
        var dir = Directory.CreateTempSubdirectory("eq-kept-").FullName;
        try
        {
            var ts = Path.Combine(dir, "ts");
            var output = Path.Combine(dir, "out");
            Directory.CreateDirectory(ts);
            Directory.CreateDirectory(output);
            test(bun, ts, output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
