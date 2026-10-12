using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The folder a bundle is made in, through the REAL bundler and bun. bun names each module by its path
/// from the folder it runs in and mixes those names into the hash a shared chunk is named by. A bundler
/// that let bun inherit its caller's folder renamed every app's chunks the day the SDK began running
/// eqc in the folder of its response file instead of the project's (#836): nothing failed, and the
/// browser fetched every chunk again.
/// </summary>
public class ModuleBundlerFolderTests
{
    [SkippableFact]
    public void TwoCopiesOfOneApp_BundleToTheSameBytes_WhereverEachOneSits()
    {
        var bun = JsExecutor.RequireBun();
        var first = Directory.CreateTempSubdirectory("eq-bundle-folder-").FullName;
        var second = Directory.CreateTempSubdirectory("eq-bundle-folder-").FullName;
        try
        {
            var bundled = Bundled(bun, first);

            bundled.Select(file => file.Name).Should().Contain(name => name != "First.js" && name != "Second.js",
                "the module both pages import is a chunk of its own, named by a hash, or nothing here could differ");
            Bundled(bun, second).Should().Equal(bundled,
                "the same app bundles to the same files, wherever it sits and wherever its caller runs");
        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }

    /// <summary>Two pages that share a module, bundled in <paramref name="project"/>: every file written.</summary>
    private static List<(string Name, string Text)> Bundled(string bun, string project)
    {
        var ts = Path.Combine(project, "obj", "eQuantic", "ts");
        var output = Path.Combine(project, "out");
        Directory.CreateDirectory(ts);
        File.WriteAllText(Path.Combine(ts, "Shared.ts"), "export const shared = (n: number): number => n * 2 + 1;\n");
        string Page(string name, int value)
        {
            var path = Path.Combine(ts, $"{name}.ts");
            File.WriteAllText(path, $"import {{ shared }} from './Shared';\nexport const {name.ToLowerInvariant()} = shared({value});\n");
            return path;
        }

        ModuleBundler.Bundle(bun, project, [Page("First", 1), Page("Second", 2)], output, ts, SourceMapMode.None)
            .Should().BeNull();

        return Directory.GetFiles(output)
            .Order(StringComparer.Ordinal)
            .Select(file => (Path.GetFileName(file), File.ReadAllText(file)))
            .ToList();
    }
}
