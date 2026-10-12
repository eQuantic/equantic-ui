using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// What the build refuses of a referenced project, and where it says so (#624). A type of one is
/// compiled when the browser code reaches it, by the rules the app's own types are compiled by, so
/// what it cannot carry to the browser is the diagnostic the same construct raises in the app, at the
/// referenced project's own file. And a type nothing reaches is never compiled, so it raises nothing
/// and its name collides with nothing.
/// </summary>
public sealed class ReferencedProjectRefusalTests
{
    /// <summary>A page that runs <paramref name="expression"/>, a string, in the browser.</summary>
    private static string Home(string expression, string usings = "") =>
        $$"""
        using eQuantic.UI.Primitives;
        {{usings}}
        namespace App.Screens;

        public sealed class Home : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                Text({{expression}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    [Fact]
    public void AReachedClassThatReadsAFile_FailsAtTheProjectsOwnFile()
    {
        using var solution = new Solution()
            .Library("Domain", files: [("ReceiptArchive.cs", SharedLibrary.ReceiptArchive)])
            .App("App", references: ["Domain"], globalUsings: SharedLibrary.Factories, files:
            [
                // In the page's Build, which runs in the browser: the class is reached.
                ("Screens/Home.cs", Home("""new ReceiptArchive("receipts").Holds("r-1.txt") ? "kept" : "gone" """, "using Domain;")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("a class that reads a file cannot run in the browser");
        built.ErrorsIn("Domain").Should().NotBeEmpty("the construct is named where it is written, in the referenced project")
            .And.OnlyContain(line => line.StartsWith(solution.FileOf("Domain", "ReceiptArchive.cs") + "("))
            .And.Contain(line => line.Contains("error EQ2004") && line.Contains("'System.IO.File.Exists'"));
        built.ErrorsIn("App").Should().BeEmpty("the page only named a class, which is no error of its own");
    }

    [Fact]
    public void TheSameClassUnreached_IsNoError()
    {
        // The control of the test above: the same solution, with the page not naming the class.
        using var solution = new Solution()
            .Library("Domain", files: [("ReceiptArchive.cs", SharedLibrary.ReceiptArchive)])
            .App("App", references: ["Domain"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("\"plain\"")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty();
        built.Modules.Should().BeEquivalentTo(["Home"]);
    }

    [Fact]
    public void AServerOnlyTypeOfAReferencedProject_IsRefusedWhereThePageNamesIt()
    {
        using var solution = new Solution()
            .Library("Common", files:
            [
                ("Vault.cs",
                    """
                    using eQuantic.UI.Primitives;

                    namespace Common;

                    [ServerOnly]
                    public static class Vault
                    {
                        public static string Key() => Environment.GetEnvironmentVariable("VAULT_KEY") ?? "";
                    }
                    """),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("Vault.Key()", "using Common;")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("a server-only type has no twin for the browser to call");
        built.ErrorsIn("App").Should().ContainSingle()
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(")
            .And.Contain("error EQ2010");
        built.Module("Vault").Should().BeNull("a type that never crosses is no module, whoever names it");
        built.ErrorsIn("Common").Should().BeEmpty("the type is refused where it is named, and its body is never compiled");
    }

    [Fact]
    public void ANameTheAppAndAReferencedProjectBothDeclare_ReachedInBoth_IsRefusedNamingBoth()
    {
        const string Status = "namespace OWNER;\n\npublic sealed record Status(string Text);\n";
        using var solution = new Solution()
            .Library("Common", files: [("Status.cs", Status.Replace("OWNER", "Common"))])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Status.cs", Status.Replace("OWNER", "App.Model")),
                // The page builds the LIBRARY's Status, and another of the app's own.
                ("Screens/Home.cs", Home("""new Common.Status("theirs").Text + new App.Model.Status("ours").Text""")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("a module is named for its type alone, and two types cannot be one module");
        var collision = built.Errors.Split('\n').Should().ContainSingle(line => line.Contains("error EQ1005")).Subject;
        collision.Should().StartWith(solution.FileOf("Common", "Status.cs") + "(",
            "the type that arrives second is the one reached in the referenced project");
        collision.Should().Contain("two types are named 'Status'").And.Contain(Path.Combine("Model", "Status.cs"),
            "the message names the declaration the name was taken by");
    }

    [Fact]
    public void OneFullName_InTheAppAndInAReferencedProject_BothReached_IsTheCollisionToo()
    {
        // Same namespace, same name, two assemblies: two types. Claimed by their full name alone they
        // would read as ONE type declared twice, which is a warning and a module that holds one of
        // the two: the page would call the app's Guard where the library's code meant its own.
        const string Guard = "namespace Shared;\n\ninternal static class Guard\n{\n    public static string Tag() => \"OWNER\";\n}\n";
        using var solution = new Solution()
            .Library("Common", files:
            [
                ("Guard.cs", Guard.Replace("OWNER", "common")),
                ("Stamp.cs", "namespace Common;\n\npublic static class Stamp\n{\n    public static string Of() => Shared.Guard.Tag();\n}\n"),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Shared/Guard.cs", Guard.Replace("OWNER", "app")),
                ("Screens/Home.cs", Home("Shared.Guard.Tag() + Common.Stamp.Of()")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("the library's Stamp reaches the library's Guard, and the page reaches the app's");
        built.Errors.Split('\n').Should().ContainSingle(line => line.Contains("error EQ1005"))
            .Which.Should().StartWith(solution.FileOf("Common", "Guard.cs") + "(");
        built.Errors.Should().NotContain("EQ1006", "two types of one full name are not one type declared twice");
    }

    [Fact]
    public void TheSameName_WithTheProjectsNeverReached_IsTheAppsModule()
    {
        const string Status = "namespace OWNER;\n\npublic sealed record Status(string Text)\n{\n    public string Owner => \"OWNER\";\n}\n";
        using var solution = new Solution()
            .Library("Common", files: [("Status.cs", Status.Replace("OWNER", "Common"))])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Status.cs", Status.Replace("OWNER", "App.Model")),
                ("Screens/Home.cs", Home("""new App.Model.Status("ours").Owner""")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty("a type nothing reaches collides with nothing");
        built.Module("Status").Should().Contain("'App.Model'").And.NotContain("'Common'");
    }

    [Fact]
    public void ANameBoundToAPackagesType_NeverImportsAReferencedProjectsModuleOfThatName()
    {
        // The page names System.Text.StringBuilder, which the runtime has. A referenced project
        // happens to declare a class of that name that nothing reaches: a name the layer of
        // referenced projects answered for by NAME alone would import the library's.
        using var solution = new Solution()
            .Library("Common", files:
            [
                ("StringBuilder.cs", "namespace Common;\n\npublic sealed class StringBuilder\n{\n    public string Text => \"common\";\n}\n"),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("""new System.Text.StringBuilder().Append("built").ToString()""")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.SiblingImportsOf("Home").Should().BeEmpty();
        built.Modules.Should().BeEquivalentTo(["Home"], "the library's class of that name is not what the page named");
    }
}
