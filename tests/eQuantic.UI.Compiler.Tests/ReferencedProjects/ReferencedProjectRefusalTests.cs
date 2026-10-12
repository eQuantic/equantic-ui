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

    /// <summary>One file of a library that declares two types of one simple name, in two
    /// namespaces. The second reads a file, so compiling it says so.</summary>
    private const string TwoStatuses =
        """
        namespace Common.Orders
        {
            public sealed record Status(string Text);
        }

        namespace Common.Payments
        {
            public sealed record Status(int Code)
            {
                public string Stored() => File.ReadAllText("status.txt");
            }
        }
        """;

    [Fact]
    public void TwoTypesOfOneNameInOneFile_OnlyTheOneReachedIsCompiled()
    {
        // A reached type is found by what it IS, never by its simple name: the file declares another
        // Status, in another namespace, that no page names. Chosen by name, both were compiled, the
        // unreached one reported what it holds and the two collided (found in review of #827).
        using var solution = new Solution()
            .Library("Common", files: [("Statuses.cs", TwoStatuses)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("""new Common.Orders.Status("open").Text""")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty("the Status of Payments is reached by nothing");
        built.Module("Status").Should().Contain("text").And.NotContain("code");
    }

    [Fact]
    public void AGenericAndANestedTypeOfAReferencedProject_AreFoundByWhatTheyAre()
    {
        // The shapes whose CLR name is not their simple name: a generic type carries its arity, a
        // nested one its owners, and a type in nested namespace blocks every one of them. A name read
        // off the symbol and one read off the declaration have to meet for each, or the type the page
        // reached is not the one the build looks for and no module is written.
        using var solution = new Solution()
            .Library("Common", files:
            [
                ("Shapes.cs",
                    """
                    namespace Common
                    {
                        namespace Shapes
                        {
                            public sealed record Box<T>(T Held);

                            public static class Outer
                            {
                                public static class Inner
                                {
                                    public static string Value => "inner";
                                }
                            }
                        }
                    }
                    """),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("new Box<int>(1).Held + Outer.Inner.Value", "using Common.Shapes;")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Box").Should().Contain("export class Box");
        built.Module("Outer$Inner").Should().Contain("'inner'");
        built.SiblingImportsOf("Home").Should().Contain(["Box", "Outer$Inner"]);
    }

    [Fact]
    public void TwoTypesOfOneNameInOneFile_BothReached_IsTheCollision()
    {
        // And when a page does reach both, they are two types wanting one module: the collision,
        // each named by its own namespace, never one type declared twice.
        using var solution = new Solution()
            .Library("Common", files: [("Statuses.cs", TwoStatuses.Replace("File.ReadAllText(\"status.txt\")", "\"stored\""))])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("""new Common.Orders.Status("open").Text + new Common.Payments.Status(2).Code""")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Should().Contain("error EQ1005").And.NotContain("EQ1006").And.NotContain("EQ2009");
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARecordThatCrossedAsData_AndHoldsServerCode_FailsAtThatMember(bool handProjects)
    {
        // The one thing that gets STRICTER for an app that already built. A record of a referenced
        // project that a Server Action answers used to cross as data alone: read from its assembly
        // it had no twin, and its answer was read back member by member. Its twin is the whole
        // record now, as an app's own record's is, so a method of it that a browser cannot run is
        // refused, at its own file, though no page calls it. Loud and at the right place, and
        // nothing is written wrong. It is filed (#826): the way through is a member the build leaves
        // out, or members compiled only where they are reached.
        using var solution = new Solution()
            .Library("Domain", files:
            [
                ("Order.cs",
                    """
                    namespace Domain;

                    public sealed record Order(decimal Total, long Number)
                    {
                        public string Export() => File.ReadAllText("orders.csv");
                    }
                    """),
            ])
            .App("App", references: ["Domain"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using Domain;

                    namespace App.Screens;

                    public sealed class Home : StatefulComponent
                    {
                        private Order? _order;

                        [ServerAction]
                        public Task<Order> Load() => Task.FromResult(new Order(1m, 2));

                        private async Task Fetch()
                        {
                            var order = await Load();
                            SetState(() => _order = order);
                        }

                        public override VisualNode Build(ComponentContext context) =>
                            Column(gap: Space.S2, children: [
                                Button("Load", onPressed: () => _ = Fetch()),
                                Text(_order is null ? "none" : _order.Total.ToString(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1),
                            ]);
                    }
                    """),
            ]);
        var built = solution.Build(handProjects);

        if (handProjects)
        {
            built.Failed.Should().BeTrue();
            built.ErrorsIn("Domain").Should().ContainSingle()
                .Which.Should().StartWith(solution.FileOf("Domain", "Order.cs") + "(")
                .And.Contain("error EQ2004").And.Contain("'System.IO.File.ReadAllText'");
            built.ErrorsIn("App").Should().BeEmpty();
        }
        else
        {
            // The control, and what the page was before: built, with the record a shape and no twin.
            built.Failed.Should().BeFalse(built.Errors);
            built.Module("Home").Should().Contain("{ members: { total: 'decimal', number: 'long' } }");
            built.Modules.Should().BeEquivalentTo(["Home"]);
        }
    }

    [Theory]
    [InlineData("a file of the library")]
    [InlineData("the list of projects")]
    public void AReferencedProjectThatCannotBeRead_StopsTheBuild_AndNeverFallsBackToGuessing(string held)
    {
        // A file is held exclusively while eqc runs, so reading it throws: a source file of the
        // library, or the list of projects itself. A failure to build the app's compilation used to
        // be survivable: the build went on by name heuristics. For a solution with referenced
        // projects that is #624 again, a page written wrong with no diagnostic, so the build stops
        // and says which list it could not stand on.
        using var solution = new Solution()
            .Library("Common", files: [("Cursor.cs", SharedLibrary.Cursor)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("new Cursor(1).Next().Position.ToString()", "using Common;")),
            ]);
        var file = held == "the list of projects"
            ? solution.FileOf("App", Path.Combine("obj", "Debug", "net10.0", "equantic.projects.txt"))
            : solution.FileOf("Common", "Cursor.cs");
        var built = solution.Build(whileEqcRuns: () =>
            new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None));

        built.Failed.Should().BeTrue();
        built.Errors.Should().Contain("error EQ0001").And.Contain("equantic.projects.txt");
        built.HasCompilation.Should().BeFalse("nothing was compiled against a compilation that is not the project's");
        built.Modules.Should().BeEmpty();
    }

    [Fact]
    public void AConstantReadOffAReferencedProjectsClass_IsInlined_AndBringsNoModule()
    {
        // The app's static class reads a const of the library's class, which is written where it is
        // read as the literal it is. Nothing in the emitted module names the library's class, so it
        // is not imported and never compiled: here it reads a file, and importing it for a literal
        // would have failed the build. Measured on a real app's `const string Serif = Theme.Serif;`,
        // which brought its whole theme class into the build.
        using var solution = new Solution()
            .Library("Common", files:
            [
                ("Theme.cs",
                    """
                    namespace Common;

                    public sealed class Theme
                    {
                        public const string Serif = "Newsreader";

                        public string Face() => File.ReadAllText("face.txt");
                    }
                    """),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Faces.cs", "using Common;\n\nnamespace App.Model;\n\npublic static class Faces\n{\n    private const string Serif = Theme.Serif;\n\n    public static string Heading => Serif;\n}\n"),
                ("Screens/Home.cs", Home("App.Model.Faces.Heading")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty();
        built.Module("Faces").Should().Contain("'Newsreader'");
        built.SiblingImportsOf("Faces").Should().BeEmpty("an inlined constant leaves no reference to the class that held it");
        built.Module("Theme").Should().BeNull();
    }

    [Fact]
    public void ANameBoundToAPackagesType_NeverImportsAReferencedProjectsModuleOfThatName()
    {
        // The page reads a member of Vendor.Status, a type of a PACKAGE. A referenced project happens
        // to declare a class of that name, which nothing reaches. The emitters gather import
        // candidates by name, so a layer that answered for a referenced project's name by NAME
        // alone would import the library's Status into a page that never named it.
        const string Status = "namespace OWNER;\n\npublic static class Status\n{\n    public static string Label => \"OWNER\";\n}\n";
        using var solution = new Solution()
            .Package("Vendor", ("Status.cs", Status.Replace("OWNER", "Vendor")))
            .Library("Common", files: [("Status.cs", Status.Replace("OWNER", "Common"))])
            .App("App", references: ["Common", "Vendor"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("Status.Label", "using Vendor;")),
            ]);
        var built = solution.Build();

        built.Module("Home").Should().Contain("Status.label", "the page names the type the code names: a package's");
        built.SiblingImportsOf("Home").Should().BeEmpty("the library's class of that name is not what the page named");
        built.Modules.Should().NotContain("Status", "and nothing reached the library's, so it is no module");
    }
}
