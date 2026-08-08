using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// The compiler's plain-JavaScript mode (TypeAnnotations = false): what a browser runs directly
/// off an import map, no bundler in the path — the playground's compile-and-mount loop. The
/// default TypeScript mode is pinned beside it so neither can drift into the other.
/// </summary>
public class PlainJavaScriptEmissionTests
{
    private const string Counter = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        [Page("/")]
        public sealed class Counter : StatefulComponent
        {
            private int _count;

            public override VisualNode Build(ComponentContext context)
            {
                var column = new Column(gap: Space.S3);
                column.Add(new Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary));
                column.Add(new Button("Up", onPressed: () => SetState(() => _count++)));
                return column;
            }
        }
        """;

    [Fact]
    public void TypeAnnotationsOff_EmitsBrowserReadyJavaScript()
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false };
        var result = compiler.CompileSource(Counter, "Counter.cs").Single();

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("export class Counter", result.TypeScript);
        Assert.Contains("from \"@equantic/runtime\"", result.TypeScript);
        // No TypeScript surface anywhere: annotations, casts, interfaces.
        Assert.DoesNotContain(": BuildContext", result.TypeScript);
        Assert.DoesNotContain(": string", result.TypeScript);
        Assert.DoesNotContain(": number", result.TypeScript);
        Assert.DoesNotContain(" as ", result.TypeScript);
    }

    [Fact]
    public void TheDefault_StaysTypeScript()
    {
        var compiler = new ComponentCompiler();
        var result = compiler.CompileSource(Counter, "Counter.cs").Single();

        Assert.True(result.Success);
        Assert.Contains("context: BuildContext", result.TypeScript);
    }
}
