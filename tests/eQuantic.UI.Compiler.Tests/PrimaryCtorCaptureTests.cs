using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A C# 12 primary-constructor parameter is INSTANCE STATE, everywhere it is read — including from
/// inside a lambda, and including when the lambda CALLS it.
/// <para>
/// Roslyn models the capture as an <c>IParameterSymbol</c>, so every place that asks "is this a
/// parameter?" answers yes for something that behaves like a field. Emitting it bare compiles, the
/// page renders, and the ReferenceError arrives whenever the callback finally fires — surfacing from
/// inside the reconciler as a TypeError about something else entirely.
/// </para>
/// <para>
/// WITH REFERENCES, which is the shape an app builds in: without them nothing resolves and the
/// emitter falls back to heuristics that happened to be right. The reported case came from a real
/// build, so the test has to be one too.
/// </para>
/// </summary>
public class PrimaryCtorCaptureTests
{
    private static string TypeScriptOf(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").Single();
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    /// <summary>The reported shape, to the character: a guard that qualifies, and a lambda that
    /// calls the same parameter with arguments.</summary>
    private const string Source = """
        using System;
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class TocEntry(Action<string, bool> onHeadingSeen, string headingId)
            : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                var heading = new Text(headingId, TypeRole.BodyM);
                if (onHeadingSeen == null) return heading;
                return new InView(heading, (visible) => onHeadingSeen(headingId, visible))
                {
                    Threshold = 0.6f,
                };
            }
        }
        """;

    [Fact]
    public void ACapturedDelegate_IsCalledThroughThis_InsideALambda()
    {
        var typeScript = TypeScriptOf(Source);

        Assert.Contains("this.onHeadingSeen(this.headingId, visible)", typeScript);
        Assert.DoesNotContain("=> onHeadingSeen(", typeScript);
    }

    /// <summary>The guard above it always worked, and the two disagreeing IS the bug — one read of
    /// one field, qualified on one line and bare on the next.</summary>
    [Fact]
    public void TheGuardAndTheLambda_ReadTheSameField()
    {
        var typeScript = TypeScriptOf(Source);

        Assert.Contains("this.onHeadingSeen == null", typeScript);
    }

    /// <summary>…and from a private METHOD, which is where the reported one lives: the guard and
    /// the lambda sit in a helper the page calls, not in Build.</summary>
    [Fact]
    public void ACapturedDelegate_IsCalledThroughThis_FromAPrivateMethod()
    {
        var typeScript = TypeScriptOf("""
            using System;
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class TocEntry(Action<string, bool> onHeadingSeen, string headingId)
                : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Heading(context);

                private VisualNode Heading(ComponentContext context)
                {
                    var heading = new Text(headingId, TypeRole.BodyM);
                    if (onHeadingSeen == null || headingId.Length == 0) return heading;
                    return new InView(heading, (visible) => onHeadingSeen(headingId, visible))
                    {
                        Threshold = 0.6f,
                    };
                }
            }
            """);

        Assert.Contains("this.onHeadingSeen(this.headingId, visible)", typeScript);
        Assert.DoesNotContain("=> onHeadingSeen(", typeScript);
    }

    /// <summary>…and on a STATEFUL page, whose members go through a different emitter.</summary>
    [Fact]
    public void ACapturedDelegate_IsCalledThroughThis_OnAStatefulPage()
    {
        var typeScript = TypeScriptOf("""
            using System;
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class TocPage(Action<string, bool> onHeadingSeen, string headingId)
                : StatefulComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var heading = new Text(headingId, TypeRole.BodyM);
                    if (onHeadingSeen == null) return heading;
                    return new InView(heading, (visible) => onHeadingSeen(headingId, visible));
                }
            }
            """);

        Assert.Contains("this.onHeadingSeen(this.headingId, visible)", typeScript);
        Assert.DoesNotContain("=> onHeadingSeen(", typeScript);
    }
}
