using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// The two-regime rule for name heuristics. With an AUTHORITATIVE model (the host handed over the
/// project's real compilation — SDK build, design session), an in-tree call the model cannot bind
/// is a build error (EQ2006), never a guessed translation: guessing there is how a missing
/// reference once shipped <c>List.Add</c> as a JavaScript <c>.add</c> that died in the browser.
/// Without that promise (standalone/minimal-model hosts), the heuristics keep deciding exactly as
/// they always have.
/// </summary>
public class SemanticHardeningTests
{
    private static CompilationResult Compile(string source, bool withProjectCompilation,
        string component = "")
    {
        var compiler = new ComponentCompiler();
        if (withProjectCompilation)
        {
            var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
            compiler.SetProjectCompilation(CSharpCompilation.Create("Probe", [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable)));
        }

        var results = compiler.CompileSource(source, "Probe.cs").ToList();
        return component.Length == 0 ? results.Single() : results.Single(r => r.ComponentName == component);
    }

    private const string UnboundCall = """
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;

        public sealed class Probe : StatelessComponent
        {
            private readonly List<int> _values = new();

            public override VisualNode Build(ComponentContext context)
            {
                _values.Addd(1);
                return new Box();
            }
        }
        """;

    [Fact]
    public void UnboundCall_UnderAnAuthoritativeModel_IsAnEQ2006Error_NotACamelCaseGuess()
    {
        var result = Compile(UnboundCall, withProjectCompilation: true);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "EQ2006" && e.Message.Contains("Addd"));
    }

    [Fact]
    public void TheSameUnboundCall_WithoutTheProjectCompilation_KeepsTheHeuristic()
    {
        // No promise of completeness was made, so the historic behaviour stands: the name decides,
        // nothing reports EQ2006. (This is the standalone/playground regime.)
        var result = Compile(UnboundCall, withProjectCompilation: false);

        Assert.DoesNotContain(result.Errors, e => e.Code == "EQ2006");
    }

    [Fact]
    public void AUserMethodThatMerelySharesALinqName_IsNeverRewrittenToTheLinqForm()
    {
        // `Where` on the app's own type, resolved by symbol: it must stay a method CALL
        // (camelCased like every emitted member), not become Array.filter.
        var source = """
            using eQuantic.UI.Primitives;

            public sealed class Query
            {
                public Query Where(string clause) => this;
            }

            public sealed class Probe : StatelessComponent
            {
                private readonly Query _query = new();

                public override VisualNode Build(ComponentContext context)
                {
                    var q = _query.Where("a = 1");
                    return new Box();
                }
            }
            """;

        var probe = Compile(source, withProjectCompilation: true, component: "Probe");

        Assert.True(probe.Success, string.Join("\n", probe.Errors.Select(e => e.Message)));
        Assert.Contains(".where(", probe.TypeScript);
        Assert.DoesNotContain(".filter(", probe.TypeScript);
    }

    [Fact]
    public void AFileWhoseTypesAlsoExistInAReference_DemotesItselfToTheHeuristics()
    {
        // The SDK feeds the component LIBRARY's own sources through eqc while the library's dll
        // sits among the references — every type in such a file exists twice, its statics resolve
        // ambiguously, and "unbound" carries no signal there. The file must demote itself instead
        // of erroring: this declares eQuantic.UI.Components.Button in source while the real
        // Components.dll is referenced, and the unbindable call inside it must NOT raise EQ2006.
        var source = """
            using eQuantic.UI.Primitives;

            namespace eQuantic.UI.Components;

            public sealed class Button : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var width = Sizing.Clearly.NotBindable();
                    return new Box();
                }
            }
            """;

        var result = Compile(source, withProjectCompilation: true);

        Assert.DoesNotContain(result.Errors, e => e.Code == "EQ2006");
    }

    [Fact]
    public void ARecordMethod_WithAnUntranslatableCall_FailsTheBuild()
    {
        // The record branch used to RETURN before diagnostics were drained, so an error raised
        // while emitting a record never reached the result and the build succeeded around it.
        var source = """
            public sealed record Money(decimal Amount)
            {
                public void Ring() => System.Console.Beep();
            }
            """;

        var result = Compile(source, withProjectCompilation: true);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code is "EQ2004" or "EQ2006");
    }
}
