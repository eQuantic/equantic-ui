using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A method stays on the server when its SYMBOL says so (#1140). The rule every emitter leaves a
/// <c>[ServerOnly]</c> method out by read the method's own attribute lists by name, and two spellings
/// C# accepts carry the attribute where those lists do not show it: an alias
/// (<c>using Keep = eQuantic.UI.Primitives.ServerOnlyAttribute;</c>), and the defining half of a partial
/// method, whose attributes C# merges with those of the implementing half, the one with the body. Each
/// was written into the twin of a class, a static class, a record, a struct, a static class nested in a
/// component and a component, body included: the module named the app type the body constructs, and
/// the build refused the body's file access as the module's own code (EQ2112).
/// <para>
/// Each case is built as eqc builds an app (<see cref="Solution"/>), and compiled on its own as the
/// playground compiles it, where nothing resolves and the attribute binds to an error type that still
/// carries its name.
/// </para>
/// </summary>
public sealed class ServerOnlyMethodBySymbolTests
{
    /// <summary>What the server-only method reads: a type of the app's, which a module would import, a
    /// host-only type of its signature and the file system, which the client boundary refuses.</summary>
    private const string Body = "File.ReadAllText(new ColdStore().Path + client.BaseAddress)";

    private const string ColdStore = "public sealed class ColdStore\n{\n    public string Path => \"cold\";\n}";

    private const string Usings = "using eQuantic.UI.Primitives;\nusing Keep = eQuantic.UI.Primitives.ServerOnlyAttribute;\n";

    /// <summary>The server-only method in a spelling only its symbol reads, static where
    /// <paramref name="modifier"/> says so.</summary>
    private static string Member(string spelling, string modifier, string indent = "    ") => spelling switch
    {
        "an alias" =>
            $"{indent}[Keep] public {modifier}string LoadFromDisk(System.Net.Http.HttpClient client) => {Body};",
        "a partial method's definition" =>
            $"{indent}[ServerOnly] public {modifier}partial string LoadFromDisk(System.Net.Http.HttpClient client);\n\n"
            + $"{indent}public {modifier}partial string LoadFromDisk(System.Net.Http.HttpClient client) => {Body};",
        _ => throw new ArgumentOutOfRangeException(nameof(spelling), spelling, null),
    };

    private static readonly string[] Spellings = ["an alias", "a partial method's definition"];

    /// <summary>A type of the model, with what a page shows of the member that crosses.</summary>
    private sealed record Model(string Module, Func<string, string> Declaration, string Shown);

    /// <summary>The emitters a type of the model reaches, each by its own module.</summary>
    private static readonly Dictionary<string, Model> Models = new()
    {
        ["a class"] = new("Disk",
            spelling => $"public sealed partial class Disk\n{{\n    public int Size => 1;\n\n{Member(spelling, "")}\n}}",
            "new App.Model.Disk().Size.ToString()"),
        ["a static class"] = new("Disk",
            spelling => $"public static partial class Disk\n{{\n    public static int Size => 1;\n\n{Member(spelling, "static ")}\n}}",
            "App.Model.Disk.Size.ToString()"),
        ["a record"] = new("Note",
            spelling => $"public sealed partial record Note(string Name)\n{{\n{Member(spelling, "")}\n}}",
            "new App.Model.Note(\"n\").Name"),
        ["a struct"] = new("Spot",
            spelling => $"public partial struct Spot\n{{\n    public int X;\n\n{Member(spelling, "")}\n}}",
            "new App.Model.Spot().X.ToString()"),
    };

    /// <summary>A page whose class holds <paramref name="members"/> beside a Build that shows
    /// <paramref name="shown"/>.</summary>
    private static string Page(string members = "", string shown = "\"plain\"", string usings = "") =>
        $$"""
        {{Usings}}{{usings}}
        namespace App.Screens;

        public sealed partial class Home : StatelessComponent
        {
        {{members}}

            public override VisualNode Build(ComponentContext context) =>
                Text({{shown}}, TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);
        }
        """;

    /// <summary>The emitters a page itself reaches: a static class nested in it, and its own class.</summary>
    private static string PageWith(string where, string spelling) => where switch
    {
        "a static class nested in a component" => Page(
            $"    public static partial class Helpers\n    {{\n        public static int Size => 1;\n\n{Member(spelling, "static ", "        ")}\n    }}",
            "Helpers.Size.ToString()", "using App.Model;\n"),
        "a component" => Page(Member(spelling, ""), usings: "using App.Model;\n"),
        _ => throw new ArgumentOutOfRangeException(nameof(where), where, null),
    };

    private static readonly Dictionary<string, string> PageModules = new()
    {
        ["a static class nested in a component"] = "Home$Helpers",
        ["a component"] = "Home",
    };

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var where in Models.Keys.Concat(PageModules.Keys))
            foreach (var spelling in Spellings)
                data.Add(where, spelling);
        return data;
    }

    private static Solution App(params (string Path, string Text)[] files) =>
        new Solution().App("App", globalUsings: SharedLibrary.Factories, files: files);

    [Theory]
    [MemberData(nameof(Cases))]
    public void AServerOnlyMethodKnownByItsSymbol_IsLeftOutOfItsTwin_InAnAppBuild(string where, string spelling)
    {
        var (module, files) = Models.TryGetValue(where, out var model)
            ? (model.Module, new[]
            {
                ($"Model/{model.Module}.cs", $"{Usings}\nnamespace App.Model;\n\n{model.Declaration(spelling)}\n"),
                ("Screens/Home.cs", Page(shown: model.Shown, usings: "using App.Model;\n")),
            })
            : (PageModules[where], new[] { ("Screens/Home.cs", PageWith(where, spelling)) });
        using var solution = App([("Model/ColdStore.cs", $"namespace App.Model;\n\n{ColdStore}\n"), .. files]);
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{where}'s method marked by {spelling} never crosses:\n{built.Errors}");
        built.Errors.Should().BeEmpty();
        var twin = built.Module(module);
        twin.Should().NotBeNull($"{where} has a twin of its own");
        twin.Should().NotContain("loadFromDisk", $"{where}'s method marked by {spelling} is no member of its twin")
            .And.NotContainAny(["HttpClient", "File.", "ColdStore"], "nothing its body or its signature names crosses");
        built.SiblingImportsOf(module).Should().NotContain("ColdStore",
            "the app type only the server-only body constructs is no import of the module");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AServerOnlyMethodKnownByItsSymbol_IsLeftOutOfItsTwin_OnItsOwn(string where, string spelling)
    {
        var (module, source) = Models.TryGetValue(where, out var model)
            ? (model.Module, $"{Usings}\nnamespace App.Model;\n\n{model.Declaration(spelling)}\n\n{ColdStore}\n")
            : (PageModules[where], $"{PageWith(where, spelling)}\n\n{ColdStore}\n");

        var result = new ComponentCompiler().CompileSource(source, "Probe.cs")
            .Single(result => result.ComponentName == module);

        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(error => $"{error.Code} {error.Message}")));
        result.TypeScript.Should().NotContain("loadFromDisk", $"{where}'s method marked by {spelling} is no member of its twin")
            .And.NotContainAny(["HttpClient", "File.", "ColdStore"], "nothing its body or its signature names crosses");
    }

    /// <summary>The other members the rule leaves out, each marked through the alias: an operator and a
    /// conversion of a class and of a record, a member of an extension block, which method lowering writes,
    /// and an interface's default, which the emitters write into the twin of a class or a record that takes
    /// it. Each reads a file and constructs the app's <c>ColdStore</c>. A record that took a server-only
    /// default was refused (EQ1008) as relying on a body nothing could read, the attribute written on the
    /// default included, where a class left it out.</summary>
    public static TheoryData<string, string, string, string, string> OtherMembers() => new()
    {
        { "a class's operator", "Amount",
            "public sealed class Amount\n{\n    public int V;\n\n    [Keep] public static Amount operator +(Amount a, Amount b) => new Amount { V = File.ReadAllText(new ColdStore().Path).Length };\n}",
            "new App.Model.Amount().V.ToString()", "opAdd" },
        { "a class's conversion", "Amount",
            "public sealed class Amount\n{\n    public int V;\n\n    [Keep] public static implicit operator Amount(string s) => new Amount { V = File.ReadAllText(s + new ColdStore().Path).Length };\n}",
            "new App.Model.Amount().V.ToString()", "fromString" },
        { "a record's operator", "Note",
            "public sealed record Note(string Name)\n{\n    [Keep] public static Note operator +(Note a, Note b) => new(File.ReadAllText(new ColdStore().Path));\n}",
            "new App.Model.Note(\"n\").Name", "opAdd" },
        { "a record's conversion", "Note",
            "public sealed record Note(string Name)\n{\n    [Keep] public static explicit operator Note(int n) => new(File.ReadAllText(new ColdStore().Path + n));\n}",
            "new App.Model.Note(\"n\").Name", "fromInt" },
        { "an extension block's member", "Exts",
            "public static class Exts\n{\n    extension(string s)\n    {\n        public string Plain() => s + \"?\";\n\n        [Keep] public string Disk() => File.ReadAllText(s + new ColdStore().Path);\n    }\n}",
            "\"x\".Plain()", "disk" },
        { "an interface's default a record takes", "Note",
            "public interface IReads\n{\n    [Keep] string Read() => File.ReadAllText(new ColdStore().Path);\n}\n\npublic sealed record Note(string Name) : IReads;",
            "new App.Model.Note(\"n\").Name", "read(" },
        { "an interface's default a record takes, marked as written", "Note",
            "public interface IReads\n{\n    [ServerOnly] string Read() => File.ReadAllText(new ColdStore().Path);\n}\n\npublic sealed record Note(string Name) : IReads;",
            "new App.Model.Note(\"n\").Name", "read(" },
        { "an interface's default a class takes", "Plain",
            "public interface IReads\n{\n    [Keep] string Read() => File.ReadAllText(new ColdStore().Path);\n}\n\npublic sealed class Plain : IReads\n{\n    public int V => 1;\n}",
            "new App.Model.Plain().V.ToString()", "read(" },
    };

    [Theory]
    [MemberData(nameof(OtherMembers))]
    public void AServerOnlyMemberKnownByItsSymbol_IsLeftOutOfItsTwin(string what, string module, string declaration, string shown, string member)
    {
        using var solution = App(
            ("Model/ColdStore.cs", $"namespace App.Model;\n\n{ColdStore}\n"),
            ($"Model/{module}.cs", $"{Usings}\nnamespace App.Model;\n\n{declaration}\n"),
            ("Screens/Home.cs", Page(shown: shown, usings: "using App.Model;\n")));
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{what}, marked through the alias, never crosses:\n{built.Errors}");
        built.Errors.Should().BeEmpty();
        built.Module(module).Should().NotBeNull().And.NotContain(member, $"{what} is no member of the twin")
            .And.NotContainAny(["File.", "ColdStore"], "nothing its body names crosses");
        built.SiblingImportsOf(module).Should().NotContain("ColdStore");
    }

    /// <summary>Client code that calls such a method is refused where it calls it (EQ2010), as a call
    /// to a method marked as written is: the twin holds no such member.</summary>
    [Theory]
    [InlineData("an alias")]
    [InlineData("a partial method's definition")]
    public void AClientCallToAServerOnlyMethodKnownByItsSymbol_IsRefusedWhereItIsWritten(string spelling)
    {
        using var solution = App(
            ("Model/ColdStore.cs", $"namespace App.Model;\n\n{ColdStore}\n"),
            ("Model/Disk.cs", $"{Usings}\nnamespace App.Model;\n\n{Models["a class"].Declaration(spelling)}\n"),
            ("Screens/Home.cs", Page(shown: "new App.Model.Disk().LoadFromDisk(null!)", usings: "using App.Model;\n")));
        var built = solution.Build();

        built.Failed.Should().BeTrue("the twin holds no loadFromDisk");
        built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle($"the call is one site:\n{built.Errors}")
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(")
            .And.Contain("error EQ2010").And.Contain("LoadFromDisk");
    }
}
