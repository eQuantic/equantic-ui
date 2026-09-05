using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// PLAIN-JAVASCRIPT mode has to reach the RECORD emitter too. It did not: records were emitted with
/// `declare x: string` members and `constructor(x: any = null)` whatever the caller asked for, so a
/// consumer that runs the module directly — the playground, which compiles a visitor's snippet and
/// imports the result — got `SyntaxError: Unexpected identifier` the moment the module loaded.
/// <para>
/// The shape of that failure is why this is pinned: compilation reported SUCCESS, the build panel
/// showed a module and a size, and the page rendered a blank frame. Nothing between the compiler
/// and the browser had anything to say.
/// </para>
/// </summary>
public class PlainJavaScriptRecordTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;

        public sealed record Payment(string Customer, decimal Amount, string Status);
        """;

    [Fact]
    public void PlainJavaScript_EmitsNoTypeScriptSyntax()
    {
        var result = new ComponentCompiler { TypeAnnotations = false }
            .CompileSource(Source, "Payment.cs").Single();

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.DoesNotContain("declare ", result.TypeScript);
        Assert.DoesNotContain(": any", result.TypeScript);
        Assert.DoesNotContain(": string", result.TypeScript);
        Assert.DoesNotContain("unknown)", result.TypeScript);
        // Still a real class with value semantics.
        Assert.Contains("class Payment", result.TypeScript);
        Assert.Contains("equals(", result.TypeScript);
    }

    [Fact]
    public void TypeScriptMode_KeepsItsAnnotations()
    {
        // The .ts module path is the default and must not lose its checking.
        var result = new ComponentCompiler().CompileSource(Source, "Payment.cs").Single();

        Assert.True(result.Success);
        Assert.Contains("declare customer: string", result.TypeScript);
    }

    [Fact]
    public void ARecordInsideAComponent_IsAlsoPlain()
    {
        // How a visitor actually writes it: the record nested in the page that uses it.
        var result = new ComponentCompiler { TypeAnnotations = false }.CompileSource("""
            using eQuantic.UI.Primitives;

            [Page("/")]
            public sealed class HomePage : StatelessComponent
            {
                private sealed record Row(string Name, int Score);

                public override VisualNode Build(ComponentContext context) =>
                    new Text(new Row("a", 1).Name, TypeRole.BodyM);
            }
            """, "HomePage.cs").ToList();

        Assert.All(result, r => Assert.DoesNotContain("declare ", r.TypeScript));
        Assert.All(result, r => Assert.DoesNotContain(": any", r.TypeScript));
    }
}
