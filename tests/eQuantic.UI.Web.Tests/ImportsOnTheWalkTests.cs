using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What a module's <c>./Order</c> means is said by whoever wrote <c>Order</c> (#830): each writer of a twin's name
/// records the symbol it wrote it for, and the build reaches the types those writes were of. A name some writer wrote
/// and recorded nothing for is decided as it was before, by every type of the name the declaration's symbols name, so
/// a cast of a null to a referenced project's type of the app's name reaches that type again. This holds the imports
/// still decided that way, over the shared library, the emission shapes and the fixtures, in
/// <c>imports-on-the-walk.baseline.txt</c>, which may only shrink: a new writer records its writes
/// (<c>WrittenTwins</c>) instead of a line here, and the regenerator (<c>EQ_UPDATE_IMPORTS_ON_THE_WALK=1</c>) only
/// takes lines out.
/// </summary>
public class ImportsOnTheWalkTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string BaselinePath =>
        Path.Combine(RepoRoot(), "tests", "eQuantic.UI.Web.Tests", "imports-on-the-walk.baseline.txt");

    [Fact]
    public void EveryImportOfTheLibraryAndTheShapes_IsWhatItsWritersRecorded_OrALineOfTheBaseline()
    {
        var transpiled = SharedComponentTranspilationTests.TranspiledSharedComponents();
        transpiled.Should().NotBeEmpty("the shared library and the emission shapes transpile");
        var onTheWalk = transpiled
            .SelectMany(module => module.Result.ImportsOnTheWalk.Select(name => $"{module.Result.ComponentName}: {name}"))
            .ToHashSet(StringComparer.Ordinal);

        var lines = File.Exists(BaselinePath) ? File.ReadAllLines(BaselinePath) : [];
        var baseline = lines.Where(line => line.Length > 0 && !line.StartsWith('#')).ToHashSet(StringComparer.Ordinal);

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_IMPORTS_ON_THE_WALK") == "1")
        {
            // Only ever takes lines OUT: an import whose writers all record now leaves the baseline, and a new one
            // on the walk is never written in for you.
            File.WriteAllLines(BaselinePath, lines.Where(line => line.Length == 0 || line.StartsWith('#') || onTheWalk.Contains(line)));
            return;
        }

        onTheWalk.Except(baseline).Order(StringComparer.Ordinal).Should().BeEmpty(
            "a writer wrote these twins' names and recorded no symbol, so the build decides what each import means by the "
            + "names the declaration's symbols name. Record each write where the name is written (WrittenTwins.Wrote); "
            + "the baseline only shrinks");
        baseline.Except(onTheWalk).Order(StringComparer.Ordinal).Should().BeEmpty(
            "every write of these names is on record now, so they leave the baseline: regenerate with "
            + "EQ_UPDATE_IMPORTS_ON_THE_WALK=1");
    }
}
