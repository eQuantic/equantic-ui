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
    public void AReferencedProjectsExceptionClass_IsAModuleThePageImports()
    {
        // A name ending in Exception was taken for .NET's, whose construction lowers to an Error. A
        // library's own exception class is a class of its own (#611), and the page wrote
        // `new OrderException('x').code` with no import of it.
        using var solution = new Solution()
            .Library("Common", files: [("OrderException.cs",
                "namespace Common;\n\npublic sealed class OrderException(string message) : Exception(message)\n{\n    public string Code => \"E1\";\n}\n")])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
                [("Screens/Home.cs", Page("Type").Replace("Common.Type.Family()", "new Common.OrderException(\"x\").Code"))]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("OrderException").Should().NotBeNull("the page builds the library's exception");
        built.SiblingImportsOf("Home").Should().Contain("OrderException");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AClassNamedMath_IsImportedOnlyByTheFileThatMeansIt(bool declaredByTheLibrary)
    {
        // The name is both at once here: one file calls the solution's own Math, and another calls
        // System.Math. Asked by the name alone, the second imported the solution's Math, which hides
        // JavaScript's own inside that module, and `Math.max` was then undefined. Each file's own
        // symbols say which Math it means.
        var declaredIn = declaredByTheLibrary ? "Common" : "App.Util";
        var math = Library("Math").Replace("namespace Common;", $"namespace {declaredIn};");
        var files = new List<(string Path, string Text)>
        {
            ("Screens/Home.cs", Page("Type").Replace("Common.Type.Family()", $"{declaredIn}.Math.Family()")),
            ("Screens/Other.cs", Page("Type").Replace("class Home", "class Other").Replace("Common.Type.Family()", "Math.Max(1, 2).ToString()")),
        };
        if (!declaredByTheLibrary) files.Add(("Util/Math.cs", math));
        using var solution = new Solution()
            .Library("Common", files: declaredByTheLibrary ? [("Math.cs", math)] : [("Nothing.cs", "namespace Common;\n\npublic static class Nothing\n{\n}\n")])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [.. files]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.SiblingImportsOf("Home").Should().Contain("Math", "this file calls the solution's Math");
        built.SiblingImportsOf("Other").Should().NotContain("Math", "this file calls System.Math, and the import would hide JavaScript's");
        built.Module("Other").Should().Contain("Math.max(1, 2)");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnExtensionDeclaredInAClassNamedMath_IsImported_ThoughTheSourceNamesNoMath(bool declaredByTheLibrary)
    {
        // The page writes `2.Twice()`, and the conversion writes `Math.twice(2)`: the name is in the
        // module and nowhere in its source, so no identifier of the source can be asked what it means.
        // The conversion introduced it for a type of the solution, by that type's symbol.
        var declaredIn = declaredByTheLibrary ? "Common" : "App.Util";
        var math = $"namespace {declaredIn};\n\npublic static class Math\n{{\n    public static int Twice(this int value) => value * 2;\n}}\n";
        var page = Page("Type").Replace("namespace App.Screens;", $"using {declaredIn};\n\nnamespace App.Screens;")
            .Replace("Common.Type.Family()", "2.Twice().ToString()");
        var files = new List<(string Path, string Text)> { ("Screens/Home.cs", page) };
        if (!declaredByTheLibrary) files.Add(("Util/Math.cs", math));
        using var solution = new Solution()
            .Library("Common", files: declaredByTheLibrary ? [("Math.cs", math)] : [("Nothing.cs", "namespace Common;\n\npublic static class Nothing\n{\n}\n")])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [.. files]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain("Math.twice(2)");
        built.SiblingImportsOf("Home").Should().Contain("Math", "the module names Math, which the conversion wrote for the solution's class");
    }

    [Fact]
    public void ANameWithNoModuleBehindIt_IsStillDotNets()
    {
        // The control, and why the list stays: no class of the solution is called Math, so the name
        // is System.Math's, which lowers to JavaScript's own, and nothing is imported for it.
        using var solution = new Solution()
            .Library("Common", files: [("Type.cs", Library("Type"))])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
                [("Screens/Home.cs", Page("Type").Replace("Common.Type.Family()", "Math.Max(1, 2).ToString()"))]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Math").Should().BeNull("no class of the solution is called Math");
        built.SiblingImportsOf("Home").Should().BeEmpty();
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
