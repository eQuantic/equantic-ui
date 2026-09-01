using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>[ServerOnly]</c> on a CLASS: the type never crosses, so the parser emits no module for it.
/// <para>
/// Every top-level static class and every plain class in an app is otherwise mirrored to
/// JavaScript — a Roslyn compilation service and a hosted warm-up living in a web project failed
/// the build with EQ2004 on their first server-only call (Stopwatch, CSharpCompilation), and
/// nothing short of moving them to another assembly could say "this never ships". The method-level
/// attribute already said it for methods; this is the same sentence for a whole type.
/// </para>
/// </summary>
public class ServerOnlyClassTests
{
    private static string Source(string attribute) => $$"""
        using System.Diagnostics;
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        {{attribute}}
        public static class Warmup
        {
            public static long Measure()
            {
                var watch = Stopwatch.StartNew();
                watch.Stop();
                return watch.ElapsedMilliseconds;
            }
        }

        {{attribute}}
        public sealed class CompileService
        {
            public string Run() => Stopwatch.GetTimestamp().ToString();
        }

        [Page("/probe")]
        public sealed class ProbePage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Text("x", TypeRole.BodyM);
        }
        """;

    [Fact]
    public void AServerOnlyClass_GetsNoModule_AndItsServerCallsRaiseNothing()
    {
        var results = new ComponentCompiler().CompileSource(Source("[ServerOnly]"), "Probe.cs").ToList();

        results.Select(r => r.ComponentName).Should().BeEquivalentTo(["ProbePage"],
            "neither the static helper nor the plain class crosses to the client");
        results.SelectMany(r => r.Errors).Should().NotContain(e => e.Code == "EQ2004",
            "a class that never ships cannot fail on the server surface it uses");
    }

    [Fact]
    public void WithoutTheAttribute_BothClassesAreMirrored_AndTheServerCallsAreTheError()
    {
        // The control: the same source minus the attribute is exactly the failure the attribute
        // exists to prevent — both classes become modules, and Stopwatch has no translation.
        var results = new ComponentCompiler().CompileSource(Source(""), "Probe.cs").ToList();

        results.Select(r => r.ComponentName).Should().Contain(["Warmup", "CompileService"]);
        results.SelectMany(r => r.Errors).Should().Contain(e => e.Code == "EQ2004"
            && e.Message.Contains("[ServerOnly]"),
            "the diagnostic names the way out");
    }
}
