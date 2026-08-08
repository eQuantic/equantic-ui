using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A write-once page compiled WITHOUT project references (in-memory CompileSource — the
/// playground's exact mode): the parser must recognize the shared shape by its FORM
/// (`VisualNode Build(ComponentContext …)` on the class itself) when no semantic model can
/// resolve the base type. Falling back to the Core two-class split miscompiled the page
/// silently — nameless state class, Build body dropped for an empty Container.
/// </summary>
public class SharedPageWithoutRefsTests
{
    private static string Page(string build) => $$"""
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        [Page("/")]
        public sealed class Decl : StatefulComponent
        {
            private int _count;

            {{build}}
        }
        """;

    private const string DeclarativeBody = """
        new Column(gap: Space.S3)
        {
            Children =
            {
                new Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary),
                new Button("Up", onPressed: () => SetState(() => _count++)),
            },
        }
        """;

    [Theory]
    [InlineData("public override VisualNode Build(ComponentContext context)\n{\n    return " + "{BODY}" + ";\n}")]
    [InlineData("public override VisualNode Build(ComponentContext context) =>\n    " + "{BODY}" + ";")]
    public void BlockAndExpressionBodies_BothCompileTheSharedShape(string template)
    {
        var source = Page(template.Replace("{BODY}", DeclarativeBody));
        var result = new ComponentCompiler().CompileSource(source, "Decl.cs").Single();

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("class Decl extends SharedStatefulComponent", result.TypeScript);
        // The declarative children initializer survives as a config array.
        Assert.Contains("children: [", result.TypeScript);
        Assert.Contains("new Text(", result.TypeScript);
        Assert.Contains("new Button(", result.TypeScript);
        Assert.Contains("this.setState(", result.TypeScript);
        // None of the silent-miscompile artifacts.
        Assert.DoesNotContain("new Container({})", result.TypeScript);
        Assert.DoesNotContain("class  extends", result.TypeScript);
    }
}
