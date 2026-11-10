using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// eqc's command line, read as eqc reads it, from the arguments or from the response file the SDK hands
/// it (#836). The SDK wrote every value into a command a shell read first, and the shell cut what it
/// read as its own: an assembly name <c>Cash$Flow</c> reached eqc as <c>Cash</c>, and a backtick ran
/// as a command. A line of the file is an argument whatever it holds.
/// </summary>
public class AppBuildArgumentsTests
{
    /// <summary>What a shell reads as its own, all of it legal in a file's name on macOS and Linux.</summary>
    private const string Odd = "a $b `c` |d; 'e' \"f\" %g% *h? @i";

    [Fact]
    public void TheArgumentsOfAFile_AreItsLines_EachAsItIs()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory("eq-arguments-").FullName, "eqc.rsp");
        File.WriteAllLines(file, ["one", Odd, "", "  spaced  "]);

        AppBuildOptions.ReadArguments(["@" + file]).Should().Equal(["one", Odd, "  spaced  "],
            "a line is an argument whatever it holds, and an empty line is none");
    }

    [Fact]
    public void ACommandLineThatNamesNoFile_IsItsOwnArguments()
    {
        AppBuildOptions.ReadArguments(["src", "out", "--refs", "refs.txt"])
            .Should().Equal(["src", "out", "--refs", "refs.txt"]);
        AppBuildOptions.ReadArguments(["src", "@file"]).Should().Equal(["src", "@file"],
            "only the first argument names a file");
    }

    /// <summary>
    /// The arguments after the file follow its own: the server of an app in development starts eqc with
    /// the file the build wrote and <c>--serve</c> after it, so it serves the build's own arguments
    /// (#1276).
    /// </summary>
    [Fact]
    public void TheArgumentsAfterTheFile_FollowItsOwn()
    {
        var file = Path.Combine(Path.GetTempPath(), $"eq-arguments-{Guid.NewGuid():N}.rsp");
        File.WriteAllLines(file, ["src", "out", "--refs", "refs.txt"]);
        try
        {
            AppBuildOptions.ReadArguments(["@" + file, "--serve"])
                .Should().Equal(["src", "out", "--refs", "refs.txt", "--serve"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void AFileThatIsNotThere_IsAnErrorThatNamesIt()
    {
        var missing = Path.Combine(Path.GetTempPath(), "eq-arguments-missing", "eqc.rsp");

        var read = () => AppBuildOptions.ReadArguments(["@" + missing]);

        read.Should().Throw<IOException>();
    }

    [Fact]
    public void EveryOption_IsReadAsEqcReadsIt()
    {
        var options = AppBuildOptions.FromArguments([
            "/app; /components ;/charts", "/app/wwwroot/_equantic/",
            "--refs", "/app/obj/refs.txt",
            "--projects", "/app/obj/projects.txt",
            "--assembly-name", "Shop.Site",
            "--ref-sources", "/app/obj/ref-sources.txt",
            "--generated", "/app/generated",
            "--bun", "/tools/bun",
            "--source-maps", "external",
            "--serve",
        ]);

        options.SourceDirectories.Should().Equal(["/app", "/components", "/charts"]);
        options.OutputDirectory.Should().Be("/app/wwwroot/_equantic/");
        options.ReferencesFile.Should().Be("/app/obj/refs.txt");
        options.ProjectsFile.Should().Be("/app/obj/projects.txt");
        options.AssemblyName.Should().Be("Shop.Site");
        options.ReferenceSourcesFile.Should().Be("/app/obj/ref-sources.txt");
        options.GeneratedDirectory.Should().Be("/app/generated");
        options.BunPath.Should().Be("/tools/bun");
        options.SourceMaps.Should().Be(SourceMapMode.External);
        options.Serve.Should().BeTrue();
    }

    [Fact]
    public void AnAssemblyNamedLikeTheServeFlag_IsANameAndNoFlag()
    {
        // Found anywhere in the list, a flag as an assembly's name put the CLI in a mode that stays, and
        // the SDK's build compiled once and then waited forever (found in review of #843).
        var named = AppBuildOptions.FromArguments(["src", "out", "--assembly-name", "--serve"]);
        var serving = AppBuildOptions.FromArguments(["src", "out", "--serve", "--assembly-name", "App"]);

        named.AssemblyName.Should().Be("--serve");
        named.Serve.Should().BeFalse();
        serving.Serve.Should().BeTrue();
        serving.AssemblyName.Should().Be("App");
    }

    [Fact]
    public void AValueAShellWouldRead_ArrivesAsItIs_ThroughTheFile()
    {
        var directory = Directory.CreateTempSubdirectory("eq-arguments-").FullName;
        var file = Path.Combine(directory, "eqc.rsp");
        File.WriteAllLines(file, [
            $"/{Odd}/app", $"/{Odd}/app/wwwroot/_equantic/",
            "--refs", $"/{Odd}/refs.txt",
            "--assembly-name", "Cash$Flow",
            "--source-maps", "none",
        ]);

        var options = AppBuildOptions.FromArguments(AppBuildOptions.ReadArguments(["@" + file]));

        options.AssemblyName.Should().Be("Cash$Flow");
        options.ReferencesFile.Should().Be($"/{Odd}/refs.txt");
        options.OutputDirectory.Should().Be($"/{Odd}/app/wwwroot/_equantic/");
        // The roots are separated by a semicolon, the one character a root cannot hold: MSBuild
        // itself reads it as a separator of items.
        options.SourceDirectories.Should().Equal([$"/{Odd.Split(';')[0]}", $"{Odd.Split(';')[1].Trim()}/app"]);
    }

    [Fact]
    public void AValueThatReadsLikeAnOption_IsTheValueItStandsAs()
    {
        // Read in order, an option and then its value. Taken by where its text first appears, the
        // value `--bun` was read as the option.
        var options = AppBuildOptions.FromArguments(["src", "out", "--assembly-name", "--bun", "--bun", "/tools/bun"]);

        options.AssemblyName.Should().Be("--bun");
        options.BunPath.Should().Be("/tools/bun");
    }

    [Theory]
    [InlineData(new[] { "only-one" }, "the source roots and the output folder first")]
    [InlineData(new[] { "--refs", "refs.txt" }, "the source roots and the output folder first")]
    [InlineData(new[] { "src", "out", "--refs" }, "--refs is handed no value")]
    [InlineData(new[] { "src", "out", "--source-maps", "some" }, "--source-maps takes full, external or none")]
    public void ACommandLineThatDoesNotHold_IsAnArgumentError(string[] arguments, string because)
    {
        var read = () => AppBuildOptions.FromArguments(arguments);

        read.Should().Throw<ArgumentException>().WithMessage($"*{because}*");
    }
}
