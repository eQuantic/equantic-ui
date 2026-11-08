using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// eqc compiles what the C# compiler compiled: each project's Compile items and the symbols its
/// <c>DefineConstants</c> defined, the app's and each referenced project's alike (#828, #1003). It walked a
/// project's directory and parsed with no symbol: <c>#if DEBUG</c> took the branch neither configuration
/// builds, a file the project removes became a module, a file it links from outside was never read, and a
/// Release build after a Debug one read both configurations' generated usings.
/// </summary>
public sealed class WhatCSharpCompiledTests
{
    private const string Flags =
        """
        namespace App;

        public static class Flags
        {
            public static string Read()
            {
        #if DEBUG
                return "debug";
        #elif NET10_0_OR_GREATER
                return "net10";
        #else
                return "neither";
        #endif
            }
        }
        """;

    private static string Page(string call) =>
        $$"""
        using eQuantic.UI.Primitives;

        namespace App;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text({{call}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    private static Solution App(params (string Path, string Text)[] files) =>
        new Solution()
            .Library("Common", files: [("Nothing.cs", "namespace Common;\n\npublic static class Nothing\n{\n}\n")])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: files);

    [Theory]
    [InlineData("DEBUG;TRACE;NET10_0_OR_GREATER", "'debug'")]
    [InlineData("RELEASE;TRACE;NET10_0_OR_GREATER", "'net10'")]
    public void TheAppsSymbols_PickTheBranchItsBuildCompiled(string symbols, string branch)
    {
        using var solution = App(("Model/Flags.cs", Flags), ("Screens/Home.cs", Page("Flags.Read()"))).Defines("App", symbols);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Flags").Should().Contain($"return {branch};").And.NotContain("'neither'");
    }

    [Fact]
    public void AReferencedProjectsSymbols_AreItsOwn()
    {
        using var solution = new Solution()
            .Library("Common", globalUsings: SharedLibrary.Factories, files:
                [("Edition.cs", "namespace Common;\n\npublic static class Edition\n{\n#if PRO\n    public static string Name() => \"pro\";\n#else\n    public static string Name() => \"free\";\n#endif\n}\n")])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
                [("Screens/Home.cs", Page("Common.Edition.Name()").Replace("namespace App;", "namespace App;\n"))])
            .Defines("Common", "PRO;TRACE")
            .Defines("App", "DEBUG;TRACE");
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Edition").Should().Contain("'pro'", "the library defines PRO, which the app does not")
            .And.NotContain("'free'");
    }

    [Fact]
    public void AFileTheProjectRemoves_IsNoModule_AndAFileItLinks_IsOne()
    {
        using var solution = App(
                ("Screens/Home.cs", Page("Linked.Hi()")),
                ("Old/Retired.cs", "namespace App;\n\npublic static class Retired\n{\n    public static string Was() => \"was\";\n}\n"))
            .Removes("App", "Old/Retired.cs")
            .Links("App", "Shared/Linked.cs", "namespace App;\n\npublic static class Linked\n{\n    public static string Hi() => \"hi\";\n}\n");
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Retired").Should().BeNull("the project removes the file, so csc never compiled it");
        built.Module("Linked").Should().NotBeNull("the project links the file, and the page calls it");
        built.SiblingImportsOf("Home").Should().Contain("Linked");
    }

    [Fact]
    public void TheGeneratedUsings_AreTheConfigurationsBeingBuilt_Alone()
    {
        // A Release build after a Debug one: both configurations' generated usings are under obj, and the
        // Release compile holds its own alone (#1003).
        using var solution = App(("Screens/Home.cs", Page("Flags.Read()")), ("Model/Flags.cs", Flags))
            .Write("App", Path.Combine("obj", "Release", "net10.0", "App.GlobalUsings.g.cs"),
                "global using System;\nglobal using static eQuantic.UI.Components.UI;\n")
            .Removes("App", Path.Combine("obj", "Debug", "net10.0", "App.GlobalUsings.g.cs"));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Output.Should().NotContain("generated global-usings file", "the build reads the usings the compile holds");
        built.Output.Should().Contain("Reading the");
    }

    [Fact]
    public void WithNoListHanded_TheBuildWalksAsBefore()
    {
        // A host with no MSBuild behind it hands no list, and the directory is read as it always was.
        using var solution = App(("Model/Flags.cs", Flags), ("Screens/Home.cs", Page("Flags.Read()"))).Defines("App", "DEBUG");
        var built = solution.Build(handSources: false);

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Flags").Should().Contain("return 'neither';");
    }
}
