using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A library's <c>params</c> parameter is the array it is on both sides of a reference: its twin is written from its
/// own source, where the symbol is in source, and the page calls it through metadata, where it is not. The two
/// disagreed on the convention had either side been decided by where the symbol's source is (#1159).
/// </summary>
public sealed class AParamsParameterOfALibraryTests
{
    private const string Page = """
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace App.Screens;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text(Common.Counter.Count("a", "b").ToString(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    private const string Library = """
        namespace Common;

        public static class Counter
        {
            public static int Count(params string[] names) => names.Length;
        }
        """;

    [Fact]
    public void BothSides_TakeTheArray()
    {
        using var solution = new Solution()
            .Library("Common", files: [("Counter.cs", Library)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", Page)]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Counter").Should().Contain("count(names").And.NotContain("...names");
        built.Module("Home").Should().Contain("Counter.count(['a', 'b'])");
    }
}
