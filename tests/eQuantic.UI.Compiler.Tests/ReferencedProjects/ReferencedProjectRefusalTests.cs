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

    /// <summary>The Domain's Order: an entity with server code, which no browser can run.</summary>
    private const string DomainOrder =
        """
        namespace Domain;

        public sealed class Order
        {
            public const string Kind = "order";

            public decimal Total { get; set; }

            public static Order Stored(string id) => new() { Total = decimal.Parse(File.ReadAllText(id + ".txt")) };
        }
        """;

    /// <summary>The app's Order: the shape a page shows, under the same simple name.</summary>
    private const string AppOrder = "namespace App.Model;\n\npublic sealed record Order(decimal Total);\n";

    [Theory]
    [InlineData("[ServerAction]\n    public Task<Order> Load() => Task.FromResult(new Order(Domain.Order.Stored(\"o-1\").Total));")]
    [InlineData("[ServerAction]\n    public Task<Order> Load()\n    {\n        Domain.Order stored = Domain.Order.Stored(\"o-1\");\n        return Task.FromResult(new Order(stored.Total));\n    }")]
    [InlineData("[ServerOnly]\n    public decimal Warm() => Domain.Order.Stored(\"o-1\").Total;")]
    public void ServerCodeOfAPage_ThatNamesALibraryTypeOfTheAppsName_ReachesNothing(string serverMember)
    {
        // The usual shape of an app with a Domain: the page shows its own Order, and its server code
        // maps it from the Domain's Order, of the same simple name. A Server Action's body and a
        // [ServerOnly] method never cross, so what they name is not what the page's `./Order` means.
        // Read with the rest of the class, the Domain's Order was reached: compiled for the browser,
        // where its file IO is refused, and in collision with the app's own (found in review of #827).
        using var solution = new Solution()
            .Library("Domain", files: [("Order.cs", DomainOrder)])
            .App("App", references: ["Domain"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Order.cs", AppOrder),
                ("Screens/Home.cs",
                    $$"""
                    using eQuantic.UI.Primitives;
                    using App.Model;

                    namespace App.Screens;

                    public sealed class Home : StatefulComponent
                    {
                        private Order _order = new(0m);

                        {{serverMember}}

                        public override VisualNode Build(ComponentContext context) =>
                            Text(_order.Total.ToString(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Errors.Should().BeEmpty("the Domain's Order is named by server code alone");
        built.Module("Order").Should().Contain("total").And.NotContain("stored");
        built.Modules.Should().BeEquivalentTo(["Home", "Order"]);
    }

    [Theory]
    [InlineData("nameof in a field", "", "private static readonly string Label = nameof(Domain.Order);", "_order.Total.ToString() + Label", "'Order'")]
    [InlineData("nameof in the code that crosses", "", "", "_order.Total.ToString() + nameof(Domain.Order)", "'Order'")]
    [InlineData("a constant in an attribute", "[System.ComponentModel.Description(Domain.Order.Kind)]", "", "_order.Total.ToString()", "")]
    [InlineData("typeof in an attribute", "[System.ComponentModel.DefaultValue(typeof(Domain.Order), \"x\")]", "", "_order.Total.ToString()", "")]
    [InlineData("a constant read in the code that crosses", "", "", "_order.Total.ToString() + Domain.Order.Kind", "'order'")]
    public void CodeThatWritesNothing_ThatNamesALibraryTypeOfTheAppsName_ReachesNothing(
        string what, string onTheClass, string member, string shown, string written)
    {
        // The same two Orders, and the Domain's named where NOTHING of it is written: nameof is the
        // text of a name, an attribute is metadata, and a constant is its value. Each built on main,
        // which compiled no referenced project at all, and each had the Domain's Order reached here,
        // since the page's `./Order` could mean either: compiled for the browser, where the file it
        // reads is refused. Measured on the code as pushed for the second review round, all five.
        using var solution = new Solution()
            .Library("Domain", files: [("Order.cs", DomainOrder)])
            .App("App", references: ["Domain"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Order.cs", AppOrder),
                ("Screens/Home.cs",
                    $$"""
                    using eQuantic.UI.Primitives;
                    using App.Model;

                    namespace App.Screens;

                    {{onTheClass}}
                    public sealed class Home : StatefulComponent
                    {
                        private Order _order = new(0m);

                        {{member}}

                        public override VisualNode Build(ComponentContext context) =>
                            Text({{shown}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{what} writes nothing of the Domain's Order:\n{built.Errors}");
        built.Errors.Should().BeEmpty();
        built.Module("Order").Should().Contain("total").And.NotContain("stored");
        built.Modules.Should().BeEquivalentTo(["Home", "Order"]);
        if (written.Length > 0) built.Module("Home").Should().Contain(written, $"{what} is written as text");
    }

    [Fact]
    public void AConstantWrittenAsAReference_StillReachesTheClassThatDeclaresIt()
    {
        // The other half, and what a rule of "a constant names nothing" would break: a constant read
        // by its bare name under `using static` is NOT written as its value when a source declares
        // it. It is its twin's static, so the class has to be found, imported and compiled. The walk
        // asks the strategy that decides it, and never its own idea of a constant.
        using var solution = new Solution()
            .Library("Common", files:
            [
                ("Limits.cs", "namespace Common;\n\npublic static class Limits\n{\n    public const int Max = 3;\n}\n"),
            ])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;
                    using static Common.Limits;

                    namespace App.Screens;

                    public sealed class Home : StatelessComponent
                    {
                        public override VisualNode Build(ComponentContext context) =>
                            Text(Max.ToString(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().Contain("Limits.max");
        built.SiblingImportsOf("Home").Should().Equal("Limits");
        built.Modules.Should().BeEquivalentTo(["Home", "Limits"]);
    }

    [Fact]
    public void AServerActionsAnswer_NamedNowhereElse_IsStillReached()
    {
        // The other half: a Server Action's SIGNATURE does cross. Its stub hands the answer to the
        // record's twin, so a type named by the return type alone is imported, and has to be found.
        using var solution = new Solution()
            .Library("Common", files: [("Cursor.cs", SharedLibrary.Cursor)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs",
                    """
                    using eQuantic.UI.Primitives;

                    namespace App.Screens;

                    public sealed class Home : StatefulComponent
                    {
                        private int _position;

                        [ServerAction]
                        public Task<Common.Cursor> Load() => Task.FromResult(new Common.Cursor(3));

                        private async Task Fetch()
                        {
                            var loaded = await Load();
                            SetState(() => _position = loaded.Position);
                        }

                        public override VisualNode Build(ComponentContext context) =>
                            Column(gap: Space.S2, children: [
                                Button("Load", onPressed: () => _ = Fetch()),
                                Text(_position.ToString(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1),
                            ]);
                    }
                    """),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.SiblingImportsOf("Home").Should().Contain("Cursor");
        built.Module("Cursor").Should().Contain("export class Cursor");
    }

    /// <summary>A component that calls its own [ServerOnly] method from the code that crosses.</summary>
    private const string CallsItsOwnServerOnlyMethod =
        """
        using eQuantic.UI.Primitives;

        namespace NS;

        public sealed class Sealed : StatelessComponent
        {
            [ServerOnly]
            private string Secret() => Environment.GetEnvironmentVariable("SECRET") ?? "";

            public override VisualNode Build(ComponentContext context) =>
                Text(Secret(), TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AComponentsOwnServerOnlyMethod_CalledFromItsClientCode_IsRefused(bool inALibrary)
    {
        // A component leaves a [ServerOnly] method out of its twin. Called from the component's own
        // Build, it was written as `this.secret()`, a member the twin does not have, and the build
        // said nothing: the fence was silent for any symbol of the compilation in hand, the member
        // as much as the type. In the app's own project, and in a referenced project's component now
        // that one is compiled (found in review of #827).
        using var solution = new Solution();
        if (inALibrary)
            solution.Library("Common", globalUsings: SharedLibrary.Factories, files: [("Sealed.cs", CallsItsOwnServerOnlyMethod.Replace("NS", "Common"))])
                .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
                [
                    ("Screens/Home.cs", "using eQuantic.UI.Primitives;\n\nnamespace App.Screens;\n\npublic sealed class Home : StatelessComponent\n{\n    public override VisualNode Build(ComponentContext context) => new Common.Sealed();\n}\n"),
                ]);
        else
            solution.App("App", globalUsings: SharedLibrary.Factories, files: [("Screens/Sealed.cs", CallsItsOwnServerOnlyMethod.Replace("NS", "App.Screens"))]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("the twin has no such member to call");
        var where = inALibrary ? solution.FileOf("Common", "Sealed.cs") : solution.FileOf("App", "Screens/Sealed.cs");
        built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle()
            .Which.Should().StartWith(where + "(").And.Contain("error EQ2010").And.Contain("Sealed.Secret");
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

    [Theory]
    [InlineData("the list is not there")]
    [InlineData("a line is not a project")]
    [InlineData("a listed assembly is not there")]
    [InlineData("a listed project is not there")]
    public void AListOfProjectsThatDoesNotHold_StopsTheBuild_AndIsNeverReadAsFewerProjects(string broken)
    {
        // The SDK handed a list, and when eqc reads it the list does not hold. Read as "no list",
        // or as a list of the lines that do hold, the build went on with a project as an assembly:
        // a page that builds a record of it was written with no module to import, and nothing said
        // so. No file HANDED is an app that references no project. A file handed is never that
        // (found in review of #827).
        using var solution = new Solution()
            .Library("Common", files: [("Cursor.cs", SharedLibrary.Cursor)])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Screens/Home.cs", Home("new Cursor(1).Position.ToString()", "using Common;")),
            ]);
        var list = solution.FileOf("App", Path.Combine("obj", "Debug", "net10.0", "equantic.projects.txt"));
        var built = solution.Build(whileEqcRuns: () =>
        {
            switch (broken)
            {
                case "the list is not there":
                    File.Delete(list);
                    break;
                case "a line is not a project":
                    File.AppendAllText(list, "Common.dll\n");
                    break;
                case "a listed assembly is not there":
                    File.AppendAllText(list,
                        $"{Path.Combine(solution.Root, "Ghost", "Ghost.dll")}|{Path.Combine(solution.DirectoryOf("Common"), "Common.csproj")}\n");
                    break;
                case "a listed project is not there":
                    File.AppendAllText(list,
                        $"{solution.AssemblyOf("Common")}|{Path.Combine(solution.Root, "Ghost", "Ghost.csproj")}\n");
                    break;
            }
            return Stream.Null;
        });

        built.Failed.Should().BeTrue($"{broken}, and the build cannot stand on it");
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
