using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// What a page's module holds is what the build checks against the client boundary, and nothing else
/// (#816). The boundary was a validator that walked every invocation of the FILE: a stateful page whose
/// <c>[ServerAction]</c> read a file was refused (EQ2112) as client code, and so was a
/// <c>[ServerOnly]</c> member and another type declared beside the page, though none of them crosses;
/// a stateless page was never checked by it, nor a construction, a read or a method group anywhere. It
/// is asked now where the conversion writes a symbol, so the code checked is the code converted.
/// <para>
/// A <c>[ServerOnly]</c> method is left out of its twin, whatever type declares it, so client code that
/// reaches one THROUGH A CONTRACT, the interface member it implements or the method it overrides, would
/// reach a member the browser does not have. Such a call binds to the contract, which carries no
/// attribute: it built, and the browser threw a TypeError, or ran the base's method, where .NET ran the
/// server's. It is refused where it is written (EQ2010), and a call that can reach no such method is not.
/// </para>
/// <para>
/// Each case is built as eqc builds an app (<see cref="Solution"/>), in both bodies a member can have
/// and on both kinds of page.
/// </para>
/// </summary>
public sealed class ServerCodeOfAPageTests
{
    private static readonly string[] Kinds = ["StatefulComponent", "StatelessComponent"];

    /// <summary>A page of <paramref name="kind"/> with <paramref name="members"/> beside a Build that
    /// shows <paramref name="shown"/>, a string the browser computes, and whatever
    /// <paramref name="after"/> declares after it in the same file.</summary>
    private static string Page(string kind, string members = "", string shown = "\"plain\"", string usings = "",
        string after = "") =>
        $$"""
        using eQuantic.UI.Primitives;
        {{usings}}
        namespace App.Screens;

        public sealed class Home : {{kind}}
        {
        {{members}}

            public override VisualNode Build(ComponentContext context) =>
                Text({{shown}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        {{after}}
        """;

    private static Solution App(params (string Path, string Text)[] files) =>
        new Solution().App("App", globalUsings: SharedLibrary.Factories, files: files);

    private static string[] Lines(Built built) => built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The server code a page may hold, each in the body form the issue measured and in the
    /// other one.</summary>
    public static TheoryData<string, string, string> ServerCode()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var kind in Kinds)
        {
            data.Add(kind, "a Server Action, expression-bodied",
                "    [ServerAction]\n    public Task<bool> Archived() =>\n        Task.FromResult(File.Exists(Path.Combine(Path.GetTempPath(), \"receipt-1.txt\")));");
            data.Add(kind, "a Server Action, block-bodied",
                "    [ServerAction]\n    public Task<bool> Archived()\n    {\n        var path = Path.Combine(Path.GetTempPath(), \"receipt-1.txt\");\n        return Task.FromResult(new FileInfo(path).Exists);\n    }");
            data.Add(kind, "a [ServerOnly] member, expression-bodied",
                "    [ServerOnly]\n    public bool Warm() => File.Exists(\"receipt-1.txt\");");
            data.Add(kind, "a [ServerOnly] member, block-bodied",
                "    [ServerOnly]\n    public string Warm()\n    {\n        using var client = new System.Net.Http.HttpClient();\n        System.Threading.Thread.Sleep(1);\n        return File.ReadAllText(Path.DirectorySeparatorChar + \"receipt-1.txt\");\n    }");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ServerCode))]
    public void ServerCodeOfAPage_ThatReachesTheServerPlatform_Builds(string kind, string what, string member)
    {
        using var solution = App(("Screens/Home.cs", Page(kind, member)));
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{what} never crosses, so nothing in it is client code:\n{built.Errors}");
        built.Errors.Should().BeEmpty();
        built.Module("Home").Should().NotBeNull().And.NotContainAny(["File", "Path", "HttpClient", "Thread"],
            "the module holds no server code, a Server Action's stub at most");
    }

    /// <summary>A framework type marked <c>[ServerOnly]</c>, read by server code of a page, is no type
    /// position of the page's module: the scan that finds one read the Server Action's body and the
    /// <c>[ServerOnly]</c> member with the rest of the class, and refused it (EQ2010).</summary>
    [Theory]
    [InlineData("StatefulComponent", "    [ServerAction]\n    public Task<float> Load() => Task.FromResult(Matrix2D.Identity.M11);")]
    [InlineData("StatelessComponent", "    [ServerOnly]\n    public float Warm()\n    {\n        var identity = Matrix2D.Identity;\n        return identity.M11;\n    }")]
    public void AHostOnlyFrameworkTypeReadByServerCode_IsNoTypePositionOfThePage(string kind, string member)
    {
        using var solution = App(("Screens/Home.cs", Page(kind, member)));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Home").Should().NotContain("Matrix2D");
    }

    [Theory]
    [InlineData("StatefulComponent")]
    [InlineData("StatelessComponent")]
    public void AServerOnlyClassBesideAPageInItsFile_IsNoClientCodeOfThePage(string kind)
    {
        using var solution = App(("Screens/Home.cs", Page(kind, after:
            "\n[ServerOnly]\npublic static class Disk\n{\n    public static bool Has(string path) => File.Exists(path);\n}\n")));
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Modules.Should().BeEquivalentTo(["Home"], "a [ServerOnly] class has no module");
    }

    /// <summary>A class, a static class and a record that become modules leave a [ServerOnly] member
    /// out of their twin, as a component does. Each was written, and the server code in it refused
    /// as the module's (EQ2004).</summary>
    [Theory]
    [InlineData("a class's method", "Disk", "public sealed class Disk\n{\n    public int Size => 1;\n\n    [ServerOnly]\n    public bool Has(string path) => File.Exists(path);\n}", "new App.Model.Disk().Size.ToString()", "has(")]
    [InlineData("a static class's method", "Disk", "public static class Disk\n{\n    public static int Size => 1;\n\n    [ServerOnly]\n    public static bool Has(string path) => File.Exists(path);\n}", "App.Model.Disk.Size.ToString()", "has(")]
    [InlineData("a record's method", "Note", "public sealed record Note(string Path)\n{\n    [ServerOnly]\n    public string Read() => File.ReadAllText(Path);\n}", "new App.Model.Note(\"p\").Path", "read(")]
    [InlineData("a record's operator", "Note", "public sealed record Note(string Path)\n{\n    [ServerOnly]\n    public static Note operator +(Note a, Note b) => new(File.ReadAllText(a.Path + b.Path));\n}", "new App.Model.Note(\"p\").Path", "opAdd(")]
    public void AServerOnlyMemberOfAModule_IsLeftOutOfItsTwin(string what, string type, string declaration, string shown, string member)
    {
        using var solution = App(
            ($"Model/{type}.cs", $"using eQuantic.UI.Primitives;\n\nnamespace App.Model;\n\n{declaration}\n"),
            ("Screens/Home.cs", Page("StatelessComponent", shown: shown)));
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{what} never crosses:\n{built.Errors}");
        built.Module(type).Should().NotBeNull().And.NotContain(member, $"{what} is no member of the twin");
    }

    /// <summary>Client code that reaches what only a server has, one site each, on both kinds of page.</summary>
    public static TheoryData<string, string, string, string, string, string> ClientCode()
    {
        const string describe = "    private static string Describe(object value) => \"seen\";";
        var data = new TheoryData<string, string, string, string, string, string>();
        foreach (var kind in Kinds)
        {
            data.Add(kind, "a call in Build", "", "", "File.Exists(\"x\") ? \"y\" : \"n\"", "EQ2112");
            data.Add(kind, "a construction", "", describe, "Describe(new FileInfo(\"x\"))", "EQ2112");
            data.Add(kind, "a static read", "", "", "Path.DirectorySeparatorChar.ToString()", "EQ2112");
            data.Add(kind, "a call through using static", "using static System.IO.File;", "", "Exists(\"x\") ? \"y\" : \"n\"", "EQ2112");
            data.Add(kind, "a method group", "", "    private static readonly Func<string, bool> Has = File.Exists;", "Has(\"x\") ? \"y\" : \"n\"", "EQ2112");
            data.Add(kind, "an OS thread", "", "    private static int Nap()\n    {\n        System.Threading.Thread.Sleep(1);\n        return 1;\n    }", "Nap().ToString()", "EQ2104");
            data.Add(kind, "an HTTP client", "", describe, "Describe(new System.Net.Http.HttpClient())", "EQ2103");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ClientCode))]
    public void ClientCodeOfAPage_ThatReachesTheServerPlatform_IsRefusedOnceAtTheSite(
        string kind, string what, string usings, string members, string shown, string code)
    {
        using var solution = App(("Screens/Home.cs", Page(kind, members, shown, usings)));
        var built = solution.Build();

        built.Failed.Should().BeTrue($"{what} runs in the browser, which has none of it");
        Lines(built).Should().ContainSingle($"{what} is one site, told once and never also as untranslatable (EQ2004)")
            .Which.Should().Contain($"error {code}").And.Contain("is not available in a client component");
    }

    /// <summary>The members of a page that cross, in each body form: a field's initializer, a lambda,
    /// a local function and a private method, on both kinds of page.</summary>
    public static TheoryData<string, string, string> CrossingMembers()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var kind in Kinds)
        {
            data.Add(kind, "a field's initializer", "    private readonly bool _has = File.Exists(\"x\");");
            data.Add(kind, "a lambda", "    private readonly Func<bool> _has = () => File.Exists(\"x\");");
            data.Add(kind, "a private method, expression-bodied", "    private bool Has() => File.Exists(\"x\");");
            data.Add(kind, "a local function of a block-bodied method", "    private bool Has()\n    {\n        bool Look() => File.Exists(\"x\");\n        return Look();\n    }");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CrossingMembers))]
    public void AMemberOfAPageThatCrosses_ThatReadsAFile_IsRefused(string kind, string what, string member)
    {
        using var solution = App(("Screens/Home.cs", Page(kind, member)));
        var built = solution.Build();

        built.Failed.Should().BeTrue($"{what} crosses with the page");
        Lines(built).Should().ContainSingle()
            .Which.Should().Contain("error EQ2112").And.Contain("'System.IO.File' (file-system access)");
    }

    /// <summary>A class, a static class and a record of the app are modules a page imports, so what they
    /// run is client code too: the same code as on a page, where it was an untranslated call (EQ2004).</summary>
    [Theory]
    [InlineData("Disk", "public sealed class Disk\n{\n    public bool Has(string path) => File.Exists(path);\n}")]
    [InlineData("Disk", "public static class Disk\n{\n    public static bool Has(string path) => File.Exists(path);\n}")]
    [InlineData("Note", "public sealed record Note(string Path)\n{\n    public bool Kept() => File.Exists(Path);\n}")]
    public void AModuleOfTheApp_ThatReadsAFile_IsRefusedAsAPageIs(string type, string declaration)
    {
        using var solution = App(
            ($"Model/{type}.cs", $"namespace App.Model;\n\n{declaration}\n"),
            ("Screens/Home.cs", Page("StatelessComponent")));
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        Lines(built).Should().ContainSingle()
            .Which.Should().StartWith(solution.FileOf("App", $"Model/{type}.cs") + "(").And.Contain("error EQ2112");
    }

    /// <summary>The namespace is the test, never a prefix of a type's name: <c>System.IObservable</c>
    /// starts with <c>System.IO</c>, and its <c>Subscribe</c> was refused as file-system access. An
    /// exception of a server namespace is a value the runtime builds and tests by name (#611), so a
    /// page throws and catches one as it could, and a type position names nothing a module imports.</summary>
    [Theory]
    [InlineData("StatefulComponent")]
    [InlineData("StatelessComponent")]
    public void WhatIsNoServerApi_IsNotRefusedAsOne(string kind)
    {
        using var solution = App(("Screens/Home.cs", Page(kind, shown: "Try()", members:
            """
                private FileInfo? _info;

                private static string Of(FileInfo? info) => info is null ? "none" : "some";

                private string Try()
                {
                    try { throw new IOException("x"); }
                    catch (IOException e) { return e.Message + Of(_info); }
                }

                private IDisposable Hook(IObservable<int> source, IObserver<int> sink) => source.Subscribe(sink);
            """)));
        var built = solution.Build();

        Lines(built).Should().ContainSingle("an observable is no file, an exception is a value, and a type position names nothing")
            .Which.Should().Contain("error EQ2004").And.Contain("System.IObservable<int>.Subscribe",
                "Subscribe has no translation, which is its own error");
    }

    /// <summary>
    /// Contracts of the app, in the namespace <c>NS</c>. Each of the first four is kept on the server by
    /// a type that crosses: an interface method <c>Circle</c> implements, a base's method
    /// <c>Derived</c> overrides, one <c>Gem</c> implements explicitly and one <c>Disc</c> takes from its
    /// base. Beside them, what can reach no such method: <c>Square</c>'s and <c>Plain</c>'s own,
    /// <c>Loud</c>'s call of its base, the base's own on <c>Mute</c> and in <c>Echo</c>, which no
    /// <c>Derived</c> can be, and a contract only types that never cross keep on the server.
    /// </summary>
    private const string Contracts =
        """
        using eQuantic.UI.Primitives;

        namespace NS;

        public interface IShape
        {
            double Area();
        }

        public sealed class Circle : IShape
        {
            public double R = 1;

            [ServerOnly]
            public double Area() => R * R * 3;
        }

        public sealed class Square : IShape
        {
            public double Area() => 4;
        }

        public interface IPriced
        {
            double Price();
        }

        public sealed class Gem : IPriced
        {
            [ServerOnly]
            double IPriced.Price() => 9;
        }

        public interface IRound
        {
            double Radius();
        }

        public class Round
        {
            [ServerOnly]
            public double Radius() => 1;
        }

        public sealed class Disc : Round, IRound { }

        public class Named
        {
            public virtual string Name() => "base";
        }

        public sealed class Derived : Named
        {
            [ServerOnly]
            public override string Name() => "derived";
        }

        public sealed class Plain : Named
        {
            public override string Name() => "plain";
        }

        public sealed class Loud : Named
        {
            public override string Name() => base.Name() + "!";
        }

        public sealed class Mute : Named { }

        public sealed class Echo : Named
        {
            public string Twice() => Name() + Name();
        }

        public interface ISealed
        {
            string Seal();
        }

        [ServerOnly]
        public sealed class Vault : ISealed
        {
            [ServerOnly]
            public string Seal() => "vault";
        }

        public sealed class MarkAttribute : Attribute, ISealed
        {
            [ServerOnly]
            public string Seal() => "mark";
        }

        public sealed class Open : ISealed
        {
            public string Seal() => "open";
        }

        public interface IDescribed
        {
            string Describe();
        }
        """;

    /// <summary>The page in the app, and the contracts in the app or in a project it references, named
    /// through the namespace <c>NS</c> stands for.</summary>
    private static Solution Contracted(bool inALibrary, string kind, string members, string shown)
    {
        var space = SpaceOf(inALibrary);
        var page = Page(kind, members, shown, $"using {space};");
        var contracts = Contracts.Replace("NS", space);
        return inALibrary
            ? new Solution()
                .Library("Common", files: [("Contracts.cs", contracts)])
                .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", page)])
            : App(("Model/Contracts.cs", contracts), ("Screens/Home.cs", page));
    }

    private static string SpaceOf(bool inALibrary) => inALibrary ? "Common" : "App.Model";

    /// <summary>Client code that calls a contract a type that crosses fulfils with a <c>[ServerOnly]</c>
    /// method, on both kinds of page and in both bodies, with the contracts in the app or in a project it
    /// references; and the method each call can reach, which the twin leaves out.</summary>
    public static TheoryData<string, string, string, string, string, bool> ThroughAContract()
    {
        var data = new TheoryData<string, string, string, string, string, bool>();
        foreach (var inALibrary in new[] { false, true })
        {
            data.Add("a call through an interface", "StatelessComponent", "",
                "((IShape)new Circle()).Area().ToString()", "NS.Circle.Area", inALibrary);
            data.Add("a call through an interface, on a stateful page", "StatefulComponent", "",
                "((IShape)new Circle()).Area().ToString()", "NS.Circle.Area", inALibrary);
            data.Add("a call through a base", "StatelessComponent", "",
                "((Named)new Derived()).Name()", "NS.Derived.Name", inALibrary);
            data.Add("a call through a base, on a stateful page", "StatefulComponent", "",
                "((Named)new Derived()).Name()", "NS.Derived.Name", inALibrary);
            data.Add("a call through a parameter, block-bodied", "StatefulComponent",
                "    private static string Measure(IShape shape)\n    {\n        var area = shape.Area();\n        return area.ToString();\n    }",
                "Measure(new Square())", "NS.Circle.Area", inALibrary);
            data.Add("a method group", "StatelessComponent",
                "    private static Func<double> Measure(IShape shape) => shape.Area;",
                "Measure(new Square())().ToString()", "NS.Circle.Area", inALibrary);
            data.Add("a call through a constraint", "StatelessComponent",
                "    private static double Measure<T>(T shape) where T : IShape => shape.Area();",
                "Measure(new Square()).ToString()", "NS.Circle.Area", inALibrary);
            data.Add("an explicit implementation", "StatelessComponent", "",
                "((IPriced)new Gem()).Price().ToString()", "NS.Gem.Price", inALibrary);
            data.Add("an implementation its base declares", "StatefulComponent", "",
                "((IRound)new Disc()).Radius().ToString()", "NS.Round.Radius", inALibrary);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ThroughAContract))]
    public void ACallThroughAContractAServerOnlyMethodFulfils_IsRefusedOnceWhereThePageWritesIt(
        string what, string kind, string members, string shown, string implementation, bool inALibrary)
    {
        using var solution = Contracted(inALibrary, kind, members, shown);
        var built = solution.Build();

        built.Failed.Should().BeTrue($"{what} reaches a method the twin leaves out");
        Lines(built).Should().ContainSingle($"{what} is one site, told once and never also as untranslatable (EQ2004):\n{built.Errors}")
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(")
            .And.Contain("error EQ2010")
            .And.Contain($"'{implementation.Replace("NS", SpaceOf(inALibrary))}'", "the message names the method the browser lacks");
    }

    /// <summary>A call that can reach no <c>[ServerOnly]</c> method is free: one bound to a method that is
    /// not, a call of the base in an override, a call made on a type the method's own cannot be, inside it
    /// or through it, a contract only types that never cross keep on the server,
    /// a contract the page fulfils on the server and no client code calls (a server interface, as
    /// <c>IHandleMetadata</c> is), and server code, which never crosses.</summary>
    public static TheoryData<string, string, string, string, bool> NotThroughAContract()
    {
        var data = new TheoryData<string, string, string, string, bool>();
        foreach (var inALibrary in new[] { false, true })
        {
            data.Add("a call bound to an implementation that is not [ServerOnly]", "StatelessComponent", "",
                "new Square().Area().ToString()", inALibrary);
            data.Add("a call bound to another override that is not [ServerOnly]", "StatefulComponent", "",
                "new Plain().Name()", inALibrary);
            data.Add("a call of the base in an override", "StatelessComponent", "", "new Loud().Name()", inALibrary);
            data.Add("a call through a type no [ServerOnly] override can be", "StatelessComponent", "",
                "new Mute().Name()", inALibrary);
            data.Add("a bare call in a type no [ServerOnly] override can be", "StatefulComponent", "",
                "new Echo().Twice()", inALibrary);
            data.Add("a contract only types that never cross keep on the server", "StatelessComponent", "",
                "((ISealed)new Open()).Seal()", inALibrary);
            data.Add("a contract no client code calls", "StatelessComponent, IDescribed",
                "    [ServerOnly]\n    public string Describe() => File.ReadAllText(\"about.txt\");", "\"plain\"", inALibrary);
            data.Add("a Server Action", "StatefulComponent",
                "    [ServerAction]\n    public Task<double> Load() => Task.FromResult(((IShape)new Circle()).Area());", "\"plain\"", inALibrary);
            data.Add("a [ServerOnly] member", "StatelessComponent",
                "    [ServerOnly]\n    public string Warm()\n    {\n        IShape shape = new Circle();\n        return shape.Area() + ((Named)new Derived()).Name();\n    }",
                "\"plain\"", inALibrary);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(NotThroughAContract))]
    public void ACallThatCanReachNoServerOnlyMethod_Builds(string what, string kind, string members, string shown,
        bool inALibrary)
    {
        using var solution = Contracted(inALibrary, kind, members, shown);
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{what} reaches no method a twin leaves out:\n{built.Errors}");
        built.Errors.Should().BeEmpty();
    }

    /// <summary>A refusal through <c>?.</c> is told where it is written. The conversion rewrites the tail
    /// of a conditional access before the fence sees it, and a refusal placed at the rewritten node was
    /// told at the first line of the file, where no reader looks for it.</summary>
    [Theory]
    [InlineData("a [ServerOnly] member", "    private static string Of(Derived? named) => named?.Name() ?? \"none\";", "Of(new Derived())", "EQ2010")]
    [InlineData("a contract a [ServerOnly] method fulfils", "    private static string Of(IShape? shape) => shape?.Area().ToString() ?? \"none\";", "Of(new Square())", "EQ2010")]
    [InlineData("an API only a server has", "    private static bool Of(FileInfo? info) => info?.Exists ?? false;", "Of(null).ToString()", "EQ2112")]
    public void ARefusalThroughAConditionalAccess_IsToldWhereItIsWritten(string what, string members, string shown, string code)
    {
        using var solution = Contracted(inALibrary: false, "StatelessComponent", members, shown);
        var built = solution.Build();

        var page = solution.FileOf("App", "Screens/Home.cs");
        var line = Array.FindIndex(File.ReadAllLines(page), text => text.Contains("?.")) + 1;
        Lines(built).Should().ContainSingle($"{what} is one site:\n{built.Errors}")
            .Which.Should().StartWith($"{page}({line},", $"{what} is told on the line that writes it")
            .And.Contain($"error {code}");
    }

    /// <summary>The code of a module a page reaches is client code too: a class that calls its own
    /// virtual, which a subclass keeps on the server, is refused in its own module, where the browser ran
    /// the base's method in the subclass's place.</summary>
    [Fact]
    public void AClassThatCallsItsOwnVirtual_ThatASubclassKeepsOnTheServer_IsRefusedInItsOwnModule()
    {
        using var solution = App(
            ("Model/Teller.cs", "using eQuantic.UI.Primitives;\n\nnamespace App.Model;\n\npublic class Teller\n{\n    public virtual string Name() => \"teller\";\n\n    public string Told() => Name() + \"!\";\n}\n\npublic sealed class Quiet : Teller\n{\n    [ServerOnly]\n    public override string Name() => \"quiet\";\n}\n"),
            ("Screens/Home.cs", Page("StatelessComponent", shown: "new App.Model.Teller().Told()")));
        var built = solution.Build();

        built.Failed.Should().BeTrue("a Quiet's Told would run Teller's Name in the browser");
        Lines(built).Should().ContainSingle(built.Errors)
            .Which.Should().StartWith(solution.FileOf("App", "Model/Teller.cs") + "(")
            .And.Contain("error EQ2010").And.Contain("'App.Model.Quiet.Name'");
    }

    /// <summary>A project the app references calls its own contract, and the app fulfils it on the
    /// server: the project cannot see the app, so the contracts are the build's, read over the app and
    /// every project it references at once, and the call is refused where the project writes it.</summary>
    [Fact]
    public void ACallThroughAReferencedProjectsContract_ThatTheAppFulfilsOnTheServer_IsRefusedWhereTheProjectWritesIt()
    {
        using var solution = new Solution()
            .Library("Common", files: [("Painter.cs", "namespace Common;\n\npublic interface IShape\n{\n    double Area();\n}\n\npublic static class Painter\n{\n    public static string Paint(IShape shape) => shape.Area().ToString();\n}\n")])
            .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Circle.cs", "using Common;\nusing eQuantic.UI.Primitives;\n\nnamespace App.Model;\n\npublic sealed class Circle : IShape\n{\n    [ServerOnly]\n    public double Area() => 3;\n}\n"),
                ("Screens/Home.cs", Page("StatelessComponent", shown: "Painter.Paint(new Circle())", usings: "using App.Model;\nusing Common;")),
            ]);
        var built = solution.Build();

        built.Failed.Should().BeTrue("Painter would call a Circle's Area, which its twin leaves out");
        Lines(built).Should().ContainSingle(built.Errors)
            .Which.Should().StartWith(solution.FileOf("Common", "Painter.cs") + "(")
            .And.Contain("error EQ2010").And.Contain("'App.Model.Circle.Area'");
    }
}
