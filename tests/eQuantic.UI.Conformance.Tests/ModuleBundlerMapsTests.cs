using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The modules, chunks and maps the bundler finds in its output folder, through the REAL bundler and
/// bun. eqc's folder is replaced in place (the generated-files spec): a file it does not write is
/// removed after it has written, never before, so the folder holds every file while a compile runs
/// and a compile that fails leaves it as it found it. The bundler is that folder's one prune since
/// #1279, and it goes by what the bundle wrote, never by a file's time. It once removed every map
/// before bun started (Copilot's third review of #666).
/// </summary>
public class ModuleBundlerMapsTests
{
    /// <summary>A map an earlier bundle wrote and composed, as its folder holds it.</summary>
    private const string EarlierMap =
        """{"version":3,"sources":["../Pages/Page.cs"],"sourcesContent":[null],"mappings":"AAAA","names":[]}""";

    [SkippableFact]
    public void ABundleThatFails_LeavesEveryMapWhereItWas()
    {
        Bundling((bun, ts, output) =>
        {
            var map = Earlier(output, "Page.js.map");
            var entry = Path.Combine(ts, "Page.ts");
            File.WriteAllText(entry, "export const page = ;\n");

            ModuleBundler.Bundle(bun, ts, [entry], output, ts, SourceMapMode.Full)
                .Should().NotBeNull("bun refuses an entry that does not parse");

            File.Exists(map).Should().BeTrue("a bundle that fails leaves the folder as it found it");
            File.ReadAllText(map).Should().Be(EarlierMap);
        });
    }

    [SkippableFact]
    public void ABundle_RemovesTheMapsItDidNotWrite_AfterWriting_AndComposesOnlyItsOwn()
    {
        Bundling((bun, ts, output) =>
        {
            // A module this bundle no longer writes, whose map is not one a composition can read.
            var gone = Earlier(output, "Gone.js.map", "not a map");
            var entry = Path.Combine(ts, "Page.ts");
            File.WriteAllText(entry, "export const page = 1;\n");
            var warnings = new List<string>();

            ModuleBundler.Bundle(bun, ts, [entry], output, ts, SourceMapMode.Full, warnings.Add).Should().BeNull();

            File.Exists(gone).Should().BeFalse("a map this bundle did not write does not survive it");
            File.Exists(Path.Combine(output, "Page.js.map")).Should().BeTrue("the map bun wrote stays");
            warnings.Should().BeEmpty("only the maps this bundle wrote are composed");
        });
    }

    [SkippableFact]
    public void ABundleThatWritesNoMaps_RemovesEveryMap_AfterWriting()
    {
        Bundling((bun, ts, output) =>
        {
            var map = Earlier(output, "Page.js.map");
            var entry = Path.Combine(ts, "Page.ts");
            File.WriteAllText(entry, "export const page = 1;\n");

            ModuleBundler.Bundle(bun, ts, [entry], output, ts, SourceMapMode.None).Should().BeNull();

            File.Exists(Path.Combine(output, "Page.js")).Should().BeTrue();
            File.Exists(map).Should().BeFalse("a Release bundle leaves no Debug bundle's map behind");
        });
    }

    /// <summary>
    /// A module or a chunk this bundle no longer writes is removed after it wrote, and the runtime the
    /// SDK copies beside them stays. This is the folder's one prune since the SDK's, which went by a
    /// file's time, gave way (#1279): a module whose text did not change is not written again by eqc,
    /// and the eqc a running app keeps compiles with no MSBuild around it (#1276).
    /// </summary>
    [SkippableFact]
    public void ABundle_RemovesTheModulesItDidNotWrite_AndLeavesTheRuntime()
    {
        Bundling((bun, ts, output) =>
        {
            var gone = Earlier(output, "DeletedComponent.js", "export const gone = 1;");
            var chunk = Earlier(output, "Shared-abc123.js", "export const shared = 1;");
            var runtime = Earlier(output, "runtime.js", "export const runtime = 1;");
            var entry = Path.Combine(ts, "Page.ts");
            File.WriteAllText(entry, "export const page = 1;\n");

            ModuleBundler.Bundle(bun, ts, [entry], output, ts, SourceMapMode.None).Should().BeNull();

            File.Exists(Path.Combine(output, "Page.js")).Should().BeTrue("the module bun wrote stays");
            File.Exists(gone).Should().BeFalse("its component is gone, and this bundle did not write it");
            File.Exists(chunk).Should().BeFalse("a chunk bun renamed leaves no old name behind");
            File.Exists(runtime).Should().BeTrue("the runtime is the SDK's, never the bundle's to remove");
        });
    }

    /// <summary>A file an earlier bundle wrote: an hour old, well before any bundle the test starts.</summary>
    private static string Earlier(string output, string name, string text = EarlierMap)
    {
        var path = Path.Combine(output, name);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-1));
        return path;
    }

    private static void Bundling(Action<string, string, string> test)
    {
        var bun = JsExecutor.RequireBun();
        var dir = Directory.CreateTempSubdirectory("eq-maps-").FullName;
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
