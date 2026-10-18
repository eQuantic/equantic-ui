using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// How a test starts a program, held to the hang it exists for (#971), and the guard that keeps every
/// <c>dotnet</c> a test starts on it. A stream ends when the last process holding its write end closes
/// it, not when the program exits, so a program that leaves a child running with its output is a
/// program whose output does not end.
/// </summary>
public class ChildProcessTests
{
    private const string Owner = "tests/Shared/ChildProcess.cs";

    /// <summary>
    /// The Web suite's hang, made deterministic: the shell exits at once, and the sleep it leaves
    /// running holds its stdout for thirty seconds. The run answers within its two-second deadline,
    /// as a <see cref="TimeoutException"/> naming the command, instead of waiting out the child. The
    /// case gives it twenty seconds, so a run still waiting by then is waiting for the sleep.
    /// </summary>
    [UnixFact]
    public void AProgramThatLeavesAChildHoldingItsOutput_FailsWithinItsDeadline_InsteadOfWaitingForTheChild()
    {
        var run = Task.Run(() => ChildProcess.Run("/bin/sh", ["-c", "sleep 30 & echo done"], TimeSpan.FromSeconds(2)));

        Task.WaitAny([run], TimeSpan.FromSeconds(20)).Should().Be(0,
            "the run's deadline is two seconds, and a run still waiting after twenty is waiting for the "
            + "thirty-second sleep the shell left holding its stdout");
        (run.Exception?.InnerException).Should().BeOfType<TimeoutException>()
            .Which.Message.Should().Contain("/bin/sh -c sleep 30 & echo done", "the failure names the command");
    }

    /// <summary>
    /// The build the suite hung in, under the conditions that made it hang, on every run rather than on
    /// about every other one. <c>dotnet test</c> hands the test host
    /// <c>MSBUILDENSURESTDOUTFORTASKPROCESSES</c>, given here as well so the case does not depend on
    /// what started this host, and with it MSBuild starts each worker node on the probe's own stdout and
    /// stderr. Every project builds out of process, so the build starts a node, and under a handshake
    /// salt no other build's node carries, so the node is its own rather than an idle one an earlier
    /// build left. That node outlived the build, reusable, and the probe's read waited for it. Under the
    /// probe's environment it ends with the build, and the probe returns: were anything still holding
    /// the pipes, the probe would fail at its deadline instead, saying so.
    /// </summary>
    [Fact]
    public void AProbeBuild_LeavesNoWorkerNodeHoldingItsOutput_EvenOneStartedOnIt()
    {
        var scratch = Directory.CreateTempSubdirectory("eq-probe-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(scratch, "Probe.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(scratch, "Probe.cs"), "public static class Probe { public static int One() => 1; }\n");

            var (exitCode, output, error) = SdkProbe.Run(scratch, new Dictionary<string, string>
            {
                ["MSBUILDENSURESTDOUTFORTASKPROCESSES"] = "1",
                ["MSBUILDNOINPROCNODE"] = "1",
                ["MSBUILDNODEHANDSHAKESALT"] = Path.GetFileName(scratch),
            }, "build", "-nologo", "-v:m");

            exitCode.Should().Be(0, output + error);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    /// <summary>
    /// The places <paramref name="source"/> starts <c>dotnet</c>, by the syntax tree: a
    /// <c>ProcessStartInfo</c> created for it, a target-typed <c>new</c> handed it first, a
    /// <c>Process.Start</c> handed it first, and a <c>FileName</c> set to it. A mention in a comment or
    /// inside another string starts nothing.
    /// </summary>
    internal static int DotnetStarts(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().Count(node => node switch
        {
            ObjectCreationExpressionSyntax creation =>
                creation.Type is IdentifierNameSyntax { Identifier.ValueText: "ProcessStartInfo" }
                    or QualifiedNameSyntax { Right.Identifier.ValueText: "ProcessStartInfo" }
                && IsDotnet(creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression),
            ImplicitObjectCreationExpressionSyntax creation => IsDotnet(creation.ArgumentList.Arguments.FirstOrDefault()?.Expression),
            InvocationExpressionSyntax invocation =>
                invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Start" } start
                && start.Expression.ToString() is "Process" or "System.Diagnostics.Process"
                && IsDotnet(invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression),
            AssignmentExpressionSyntax assignment =>
                assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "FileName" }
                    or MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FileName" }
                && IsDotnet(assignment.Right),
            _ => false,
        });

    private static bool IsDotnet(ExpressionSyntax? expression) =>
        expression is LiteralExpressionSyntax { Token.ValueText: "dotnet" };

    /// <summary>
    /// A test starts <c>dotnet</c> through <see cref="ChildProcess.Dotnet"/> alone. Five copies of the
    /// same few lines started it before #971, none of them held the end of the child's streams to a
    /// deadline, and the one SdkProbe carried hung the suite. A sixth fails here, named.
    /// </summary>
    [Fact]
    public void NoTestStartsDotnetButThroughTheOwner()
    {
        var root = SdkProbe.RepoRoot();
        var separator = Path.DirectorySeparatorChar;

        var copies = Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}bin{separator}") && !file.Contains($"{separator}obj{separator}"))
            .Select(file => Path.GetRelativePath(root, file).Replace(separator, '/'))
            .Where(file => file != Owner)
            .Where(file => DotnetStarts(File.ReadAllText(Path.Combine(root, file))) > 0)
            .ToList();

        // Joined, so the failure names every copy: a collection's BeEmpty names only the first.
        string.Join(Environment.NewLine, copies).Should().BeEmpty(
            $"a test starts dotnet through {Owner} (ChildProcess.Dotnet), which starts a build that leaves "
            + "nothing holding its output and holds the end of both streams to the deadline (#971)");
        File.Exists(Path.Combine(root, Owner)).Should().BeTrue("the guard names the owner it protects");
    }

    /// <summary>The guard's own reading, both ways: every shape a start was written in is found, and
    /// a mention that starts nothing is not.</summary>
    [Theory]
    [InlineData("var s = new ProcessStartInfo(\"dotnet\") { RedirectStandardOutput = true };", 1)]
    [InlineData("var s = new System.Diagnostics.ProcessStartInfo(\"dotnet\", [\"msbuild\", \"probe.proj\"]);", 1)]
    [InlineData("ProcessStartInfo s = new(\"dotnet\");", 1)]
    [InlineData("using var p = Process.Start(\"dotnet\", \"build\");", 1)]
    [InlineData("var s = new ProcessStartInfo { FileName = \"dotnet\" };", 1)]
    [InlineData("s.FileName = \"dotnet\";", 1)]
    [InlineData("var s = new ProcessStartInfo(\"git\", \"status\");", 0)]
    [InlineData("var r = ChildProcess.Dotnet([\"build\"], TimeSpan.FromMinutes(3));", 0)]
    [InlineData("var task = new RunProgram { Program = \"dotnet\" };", 0)]
    [InlineData("// new ProcessStartInfo(\"dotnet\") started the SDK", 0)]
    [InlineData("var text = \"new ProcessStartInfo(\\\"dotnet\\\")\";", 0)]
    public void TheGuard_FindsAStartAndNothingElse(string statement, int starts)
    {
        DotnetStarts($"class C {{ void M(ProcessStartInfo s) {{ {statement} }} }}").Should().Be(starts);
    }
}
