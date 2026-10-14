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
}
