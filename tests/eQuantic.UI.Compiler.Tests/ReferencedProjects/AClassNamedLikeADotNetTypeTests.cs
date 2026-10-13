using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// A class named as .NET names one of its own types is the app's class all the same: the build writes
/// its module and the page that names it imports it (#888).
/// <para>
/// The emitter kept a list of names it took for .NET's (<c>Type</c>, <c>Uri</c>, <c>Console</c>,
/// <c>Convert</c>, <c>Math</c>, <c>Regex</c>…), and dropped the import of any name on it before it
/// asked whether the build writes a module of that name. A library's <c>static class Type</c> reached
/// the page as <c>Type.family()</c> with no import and no module: the build said nothing, and the
/// browser met a name nothing defined.
/// </para>
/// </summary>
public sealed class AClassNamedLikeADotNetTypeTests
{
    private static string Page(string name) =>
        $$"""
        using eQuantic.UI.Components;
        using eQuantic.UI.Primitives;

        namespace App.Screens;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text(Common.{{name}}.Family(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    private static string Library(string name) =>
        $$"""
        namespace Common;

        public static class {{name}}
        {
            public static string Family() => "Newsreader";
        }
        """;

    [Theory]
    [InlineData("Type")]
    [InlineData("Uri")]
    [InlineData("Console")]
    [InlineData("Convert")]
    [InlineData("Math")]
    [InlineData("Regex")]
    public void AReferencedProjectsClass_IsAModuleThePageImports(string name)
    {
        using var solution = new Solution()
            .Library("Common", files: [($"{name}.cs", Library(name))])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", Page(name))]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module(name).Should().NotBeNull($"the page calls {name}.Family(), a class the library declares");
        built.SiblingImportsOf("Home").Should().Contain(name, $"the page names {name}, and only the import defines it");
    }

    [Fact]
    public void TheAppsOwnClassNamedType_IsAModuleThePageImports()
    {
        using var solution = new Solution()
            .Library("Common", files: [("Nothing.cs", "namespace Common;\n\npublic static class Nothing\n{\n}\n")])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Page("Type").Replace("Common.Type", "App.Screens.Type")),
                ("Screens/Type.cs", Library("Type").Replace("namespace Common;", "namespace App.Screens;")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Type").Should().NotBeNull("the page calls Type.Family(), a class the app declares");
        built.SiblingImportsOf("Home").Should().Contain("Type");
    }
}
