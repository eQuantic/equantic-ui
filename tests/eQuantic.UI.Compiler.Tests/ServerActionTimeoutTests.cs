using System.Linq;
using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A Server Action states how long the browser waits for it (#1881). The runtime has always taken a
/// <c>timeoutMs</c>, but every stub was written <c>invoke('&lt;id&gt;', [args])</c> and waited the default
/// 30 seconds, so an action that took longer (a real site's took 99.6 s) was abandoned while the server
/// kept working, and the page's <c>await</c> threw. <c>[ServerAction(TimeoutSeconds = …)]</c> is read as
/// C# reads an attribute's argument, a constant, and handed to the stub; a value the stub could not
/// honour is refused where it is written (EQ2017).
/// </summary>
public class ServerActionTimeoutTests
{
    private static string Page(string actions) => $$"""
        using System.Threading.Tasks;
        using eQuantic.UI.Primitives;

        [Page("/report")]
        public sealed class Report : StatefulComponent
        {
            private const int TwoMinutes = 2 * 60;

        {{actions}}

            public override VisualNode Build(ComponentContext context) => new Text("r");
        }
        """;

    private static CompilationResult Compile(string actions) =>
        new ComponentCompiler().CompileSource(Page(actions), "Report.cs").Single(r => r.ComponentName == "Report");

    [Fact]
    public void AnActionThatStatesATimeout_IsInvokedWithIt()
    {
        var report = Compile("""
                [ServerAction(TimeoutSeconds = 120)]
                public Task<string> Run() => Task.FromResult("done");
            """);

        Assert.True(report.Success, string.Join("; ", report.Errors.Select(e => e.Message)));
        Assert.Contains("return await getServerActionsClient().invoke('Report/Run', [], { timeoutMs: 120000 })", report.TypeScript);
    }

    [Fact]
    public void AnActionThatStatesNone_OrZero_KeepsItsShape()
    {
        var report = Compile("""
                [ServerAction]
                public Task<string> Load() => Task.FromResult("done");

                [ServerAction(TimeoutSeconds = 0)]
                public Task<string> Again() => Task.FromResult("done");
            """);

        Assert.True(report.Success, string.Join("; ", report.Errors.Select(e => e.Message)));
        Assert.Contains("return await getServerActionsClient().invoke('Report/Load', [])", report.TypeScript);
        Assert.Contains("return await getServerActionsClient().invoke('Report/Again', [])", report.TypeScript);
        Assert.DoesNotContain("timeoutMs", report.TypeScript);
    }

    [Fact]
    public void AConstantIsReadAsItsValue_BesideANameAndAHydratedResult()
    {
        var report = Compile("""
                [ServerAction(Name = "Total", TimeoutSeconds = TwoMinutes)]
                public Task<decimal> Sum(int year) => Task.FromResult(1m);
            """);

        Assert.True(report.Success, string.Join("; ", report.Errors.Select(e => e.Message)));
        Assert.Contains("return $eq.hydrate(await getServerActionsClient().invoke('Report/Total', [year], { timeoutMs: 120000 }), 'decimal')",
            report.TypeScript);
    }

    [Theory]
    [InlineData("-5", "-5 seconds")]
    [InlineData("601", "601 seconds")]
    [InlineData("60 * 60", "3600 seconds")]
    public void ATimeoutTheStubCouldNotHonour_IsRefusedWhereItIsWritten(string seconds, string said)
    {
        var report = Compile($$"""
                [ServerAction(TimeoutSeconds = {{seconds}})]
                public Task<string> Run() => Task.FromResult("done");
            """);

        Assert.False(report.Success);
        var error = Assert.Single(report.Errors, e => e.Code == "EQ2017");
        Assert.Contains("'Report.Run'", error.Message);
        Assert.Contains(said, error.Message);
        // The line of the attribute's argument, so the editor points at what to change.
        Assert.Equal(9, error.Line);
    }

    [Fact]
    public void ATimeoutThroughAnAlias_OrOnAPartialMethodsDefiningHalf_IsHandedToTheStub()
    {
        const string source = """
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;
            using Rpc = eQuantic.UI.Primitives.ServerActionAttribute;

            [Page("/report")]
            public sealed partial class Report : StatefulComponent
            {
                [Rpc(TimeoutSeconds = 90)]
                public Task<string> Run() => Task.FromResult("done");

                [ServerAction(TimeoutSeconds = 45)]
                public partial Task<string> Load();

                public partial Task<string> Load() => Task.FromResult("done");

                public override VisualNode Build(ComponentContext context) => new Text("r");
            }
            """;
        var report = new ComponentCompiler().CompileSource(source, "Report.cs").Single(r => r.ComponentName == "Report");

        Assert.True(report.Success, string.Join("; ", report.Errors.Select(e => e.Message)));
        Assert.Contains("invoke('Report/Run', [], { timeoutMs: 90000 })", report.TypeScript);
        Assert.Contains("invoke('Report/Load', [], { timeoutMs: 45000 })", report.TypeScript);
    }
}
