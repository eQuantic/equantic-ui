using System.Text.Json;
using eQuantic.UI.Sdk.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The task the two SDKs start every program with (#841), against a real program: this assembly,
/// started as <see cref="ArgumentEcho"/>, writes back what it received. MSBuild's <c>Exec</c> handed its
/// command to <c>/bin/sh</c> or <c>cmd.exe</c>, which read each <c>$</c>, backtick and <c>%</c> in it
/// first, and a value that ended with a backslash closed its own quotes.
/// </summary>
public class RunProgramTests
{
    /// <summary>Every character a shell reads, and the ones the command line's own convention does: a
    /// quote, a backslash before one or at the end, an empty value, a space alone.</summary>
    private static readonly string[] Odd =
    [
        "a $b", "`c`", "|d; 'e' \"f\"", "%g%", "%PATH%", "%41%3B", "*h?", "@i", "&& j", "^k", "!l",
        "\\m\\", "n\\", "\\\"o", "p\"", "", " ", "two words", "tab\there", "line\nbreak", "ção ☃",
        "--looks-like-an-option",
    ];

    private static string Echo => typeof(ArgumentEcho).Assembly.Location;

    /// <summary>An argument as a project hands it: its text in the item's metadata, as written.</summary>
    private static ITaskItem Argument(string value, string? option = null)
    {
        var item = new TaskItem("argument");
        ((ITaskItem2)item).SetMetadataValueLiteral("Value", value);
        if (option is not null) ((ITaskItem2)item).SetMetadataValueLiteral("Option", option);
        return item;
    }

    /// <summary>The echo started with <paramref name="arguments"/> after its own path.</summary>
    private static (RunProgram Task, RecordingBuildEngine Engine) Echoing(params ITaskItem[] arguments)
    {
        var engine = new RecordingBuildEngine();
        var task = new RunProgram
        {
            BuildEngine = engine,
            Program = "dotnet",
            Arguments = [Argument(Echo), .. arguments],
            ConsoleToMSBuild = true,
        };
        return (task, engine);
    }

    /// <summary>What the echo received, read back from the lines it wrote.</summary>
    private static List<string> Received(RunProgram task) =>
        task.ConsoleOutput.Select(line => JsonSerializer.Deserialize<string>(line.ItemSpec)!).ToList();

    [Fact]
    public void EveryCharacterAShellReads_ReachesTheProgramAsOneArgument()
    {
        var (task, engine) = Echoing([.. Odd.Select(value => Argument(value))]);

        task.Execute().Should().BeTrue(string.Join("\n", engine.Errors));

        Received(task).Should().Equal(Odd, "each value is one argument, as it was written");
    }

    [Fact]
    public void AnOptionIsTwoArguments_AndAValueIsPassedEvenWhenEmpty()
    {
        var (task, engine) = Echoing(
            Argument("--force"),
            Argument("Developer ID Application: A $b (T)", option: "--sign"),
            Argument("", option: "--hardened"));

        task.Execute().Should().BeTrue(string.Join("\n", engine.Errors));

        Received(task).Should().Equal(["--force", "--sign", "Developer ID Application: A $b (T)", "--hardened", ""],
            "an empty value is an argument, as a quoted \"$(X)\" was");
    }

    [Fact]
    public void AProgramThatFailsInSilence_IsEQ4004_WithTheCommandItWasStartedWith()
    {
        var (task, engine) = Echoing(Argument("a $b"));
        task.ConsoleToMSBuild = false;
        task.EnvironmentVariables = ["EQ_ECHO_EXIT=2"];

        task.Execute().Should().BeFalse("a program that fails fails the build");

        engine.Errors.Should().ContainSingle().Which.Should().Match<(string? Code, string Message)>(error =>
            error.Code == "EQ4004" && error.Message.StartsWith("dotnet ") && error.Message.Contains(Echo)
            && error.Message.Contains("\"a $b\"") && error.Message.EndsWith("ended with exit code 2."));
    }

    [Fact]
    public void EchoOff_KeepsTheArgumentsOutOfTheLog_EvenWhenTheProgramFails()
    {
        // The notary's password is an argument of notarytool's: ToolTask logs a command line whatever
        // EchoOff says, and EQ4004 would name the arguments of a call that failed.
        var (task, engine) = Echoing(Argument("pa$$ \"word\"", option: "--password"));
        task.ConsoleToMSBuild = false;
        task.EchoOff = true;
        task.EnvironmentVariables = ["EQ_ECHO_EXIT=2"];

        task.Execute().Should().BeFalse();

        engine.Messages.Concat(engine.Errors.Select(error => error.Message))
            .Where(line => !line.StartsWith('"'))
            .Should().NotContain(line => line.Contains("pa$$"), "only the program's own output may carry it");
        engine.Errors.Should().ContainSingle().Which.Message.Should().Be("dotnet ended with exit code 2.");
    }

    [Fact]
    public void IgnoreExitCode_ToleratesAFailure_AndAProgramThatIsNotOnThisMachine()
    {
        var (failing, engine) = Echoing();
        failing.IgnoreExitCode = true;
        failing.EnvironmentVariables = ["EQ_ECHO_EXIT=3"];
        failing.Execute().Should().BeTrue("the exit code is ignored, as Exec ignored it");

        var missing = new RunProgram
        {
            BuildEngine = engine,
            Program = "eq-no-such-program-on-any-path",
            IgnoreExitCode = true,
        };
        missing.Execute().Should().BeTrue("a shell answered 127 for a program it could not find, and that was ignored too");

        engine.Errors.Should().BeEmpty();
        engine.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void AProgramThatIsNotOnThisMachine_FailsTheStep()
    {
        var engine = new RecordingBuildEngine();
        var task = new RunProgram { BuildEngine = engine, Program = "eq-no-such-program-on-any-path" };

        task.Execute().Should().BeFalse();

        engine.Errors.Should().NotBeEmpty("a program that cannot start is MSBuild's own error");
    }
}
