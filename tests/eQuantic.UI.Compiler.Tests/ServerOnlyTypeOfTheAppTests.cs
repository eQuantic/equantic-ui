using eQuantic.UI.Compiler.Tests.ReferencedProjects;
using FluentAssertions;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type of the app the build writes no module for, a top-level <c>[ServerOnly]</c> class among them,
/// is refused where client code writes its name (#823), by the rule a nested one already was (#584):
/// it, or a class it derives from, is <c>[ServerOnly]</c> or an attribute, or it is declared inside a
/// type that is. The fence said nothing of a type the compilation in hand declares, so a page that
/// called the app's own <c>Vault.Key()</c> built in silence and threw <c>Vault is not defined</c> in the
/// browser, while the same <c>Vault</c> in a referenced project was refused.
/// <para>
/// Refused exactly where the module would name a twin nothing writes, and nowhere else: a position the
/// module writes as nothing (a component's field or parameter, annotated <c>any</c>; a cast; a
/// <c>nameof</c>) is free, and so is code that never crosses (a Server Action's body, a
/// <c>[ServerOnly]</c> member).
/// </para>
/// </summary>
public sealed class ServerOnlyTypeOfTheAppTests
{
    /// <summary>Server code of the app, in the namespace <c>NS</c>: one type of each shape the build
    /// keeps out.</summary>
    private const string ServerTypes =
        """
        using eQuantic.UI.Primitives;

        namespace NS;

        [ServerOnly]
        public static class Vault
        {
            public static string Key() => Environment.GetEnvironmentVariable("VAULT_KEY") ?? "k";
            public static string Name => "vault";
        }

        [ServerOnly]
        public sealed class Ledger
        {
            public int Total => 1;
            public static Ledger Open() => new();
            public string Describe() => "ledger";
        }

        [ServerOnly]
        public struct Amount
        {
            public int V;
            public static Amount operator +(Amount a, Amount b) => new Amount { V = a.V + b.V };
        }

        [ServerOnly]
        public static class Seal
        {
            public static string Sealed(this string text) => text + "!";
        }

        [ServerOnly]
        public sealed record Secret(string V);

        [ServerOnly]
        public class Account
        {
            public int N => 1;
        }

        public sealed class Savings : Account { }

        public sealed class MarkAttribute : Attribute
        {
            public string Of => "mark";
        }
        """;

    /// <summary>A type that crosses and hands out a value of a kept-out struct.</summary>
    private const string ShopType =
        "namespace NS;\n\npublic static class Shop\n{\n    public static Amount Price() => default;\n}\n";

    /// <summary>A page of <paramref name="kind"/> with <paramref name="members"/>, whose Build shows
    /// <paramref name="shown"/>.</summary>
    private static string Page(string kind, string members, string shown, string usings) =>
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
        """;

    /// <summary>The server types in the app's own project, or in a project it references; the page in
    /// the app, naming them through the namespace <c>NS</c> stands for.</summary>
    private static Solution Of(bool inALibrary, string kind, string members, string shown, string usings = "")
    {
        var space = inALibrary ? "Common" : "App.Model";
        var page = Page(kind, members.Replace("NS", space), shown.Replace("NS", space),
            $"using {space};\n" + usings.Replace("NS", space));
        var solution = new Solution();
        return inALibrary
            ? solution
                .Library("Common", files: [("Vault.cs", ServerTypes.Replace("NS", space))])
                .App("App", references: ["Common"], globalUsings: SharedLibrary.Factories, files: [("Screens/Home.cs", page)])
            : solution.App("App", globalUsings: SharedLibrary.Factories, files:
            [
                ("Model/Vault.cs", ServerTypes.Replace("NS", space)),
                ("Screens/Home.cs", page),
            ]);
    }

    /// <summary>Every way client code writes the name of a type the build keeps out.</summary>
    public static TheoryData<string, string, string, string, string, bool> Named()
    {
        var data = new TheoryData<string, string, string, string, string, bool>();
        foreach (var inALibrary in new[] { false, true })
        {
            data.Add("a call, on a stateless page", "StatelessComponent", "", "", "Vault.Key()", inALibrary);
            data.Add("a call, on a stateful page", "StatefulComponent", "", "", "Vault.Key()", inALibrary);
            data.Add("a call, block-bodied", "StatelessComponent", "",
                "    private static string Read()\n    {\n        var key = Vault.Key();\n        return key;\n    }", "Read()", inALibrary);
            data.Add("a call qualified by its namespace", "StatelessComponent", "", "", "NS.Vault.Key()", inALibrary);
            data.Add("a static read", "StatelessComponent", "", "", "Vault.Name", inALibrary);
            data.Add("a construction", "StatelessComponent", "", "", "new Ledger().Total.ToString()", inALibrary);
            data.Add("a static factory", "StatefulComponent", "", "", "Ledger.Open().Total.ToString()", inALibrary);
            data.Add("a call through using static", "StatelessComponent", "using static NS.Vault;", "", "Key()", inALibrary);
            data.Add("a read through using static", "StatelessComponent", "using static NS.Vault;", "", "Name", inALibrary);
            data.Add("a method group", "StatelessComponent", "", "    private static readonly Func<string> Read = Vault.Key;", "Read()", inALibrary);
            data.Add("an extension", "StatelessComponent", "", "", "\"x\".Sealed()", inALibrary);
            data.Add("a [ServerOnly] record", "StatelessComponent", "", "", "new Secret(\"x\").V", inALibrary);
            data.Add("a class over a [ServerOnly] base", "StatelessComponent", "", "", "new Savings().N.ToString()", inALibrary);
            data.Add("an attribute", "StatelessComponent", "", "", "new MarkAttribute().Of", inALibrary);
            data.Add("a type test", "StatelessComponent", "", "    private static bool Is(object o) => o is Ledger;", "Is(\"o\") ? \"y\" : \"n\"", inALibrary);
            data.Add("an as", "StatelessComponent", "", "    private static bool As(object o) => o as Ledger is null;", "As(\"o\") ? \"y\" : \"n\"", inALibrary);
            data.Add("a struct's zero", "StatelessComponent", "", "", "default(Amount).V.ToString()", inALibrary);
            data.Add("a field of the struct", "StatefulComponent", "", "    private Amount _amount;", "\"plain\"", inALibrary);
            data.Add("a local of the type", "StatelessComponent", "",
                "    private static string Hold()\n    {\n        Ledger? held = null;\n        return held is null ? \"none\" : \"some\";\n    }", "Hold()", inALibrary);
            data.Add("a Server Action's answer", "StatefulComponent", "",
                "    [ServerAction]\n    public Task<Secret> Load() => Task.FromResult(new Secret(Vault.Key()));", "\"plain\"", inALibrary);
            data.Add("a Server Action's parameter", "StatelessComponent", "",
                "    [ServerAction]\n    public Task<string> Store(List<Secret> secrets) => Task.FromResult(secrets.Count.ToString());", "\"plain\"", inALibrary);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Named))]
    public void ClientCodeThatNamesAKeptOutType_IsRefusedOnceWhereThePageWritesIt(
        string what, string kind, string usings, string members, string shown, bool inALibrary)
    {
        using var solution = Of(inALibrary, kind, members, shown, usings);
        var built = solution.Build();

        built.Failed.Should().BeTrue($"{what} names a type nothing writes a module for");
        built.ErrorsIn("App").Should().ContainSingle($"{what} is one site:\n{built.Errors}")
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(")
            .And.Contain("error EQ2010").And.Contain("never reaches the browser");
        built.Modules.Should().NotContain(["Vault", "Ledger", "Amount", "Seal", "Secret", "Account", "Savings", "MarkAttribute"],
            "a type that never crosses has no module, whoever names it");
    }

    /// <summary>
    /// An instance member of a type the build writes no module for, read off a VALUE of it: no twin holds
    /// the member, and a value's position names no type (a component's field and parameter are annotated
    /// <c>any</c>, a cast is the value), so no other fence refuses it. <c>ledger.Describe()</c> over a
    /// parameter was written as <c>ledger.describe()</c>, a method nothing defines, in the app as in a
    /// referenced project, where the type-level fence had refused it before #823 (Copilot's review of
    /// #944). A member whose receiver was refused where its expression named the type
    /// (<c>new Ledger().Total</c>, <c>Ledger.Open().Total</c>) is told once, by the rows of
    /// <see cref="Named"/>.
    /// </summary>
    public static TheoryData<string, string, string, string, bool> ReadOffAValue()
    {
        var data = new TheoryData<string, string, string, string, bool>();
        foreach (var inALibrary in new[] { false, true })
        {
            data.Add("a method called through a parameter", "StatelessComponent",
                "    private static string Read(Ledger ledger) => ledger.Describe();", "Ledger.Describe", inALibrary);
            data.Add("a property read through a parameter", "StatefulComponent",
                "    private static string Read(Ledger ledger) => ledger.Total.ToString();", "Ledger.Total", inALibrary);
            data.Add("a method group through a parameter", "StatelessComponent",
                "    private static Func<string> Read(Ledger ledger) => ledger.Describe;", "Ledger.Describe", inALibrary);
            data.Add("a call through a field and ?., block-bodied", "StatefulComponent",
                "    private Ledger? _ledger;\n\n    private string Read()\n    {\n        var text = _ledger?.Describe();\n        return text ?? \"none\";\n    }",
                "Ledger.Describe", inALibrary);
            data.Add("a read through a cast", "StatelessComponent",
                "    private static int Read(object o) => ((Ledger)o).Total;", "Ledger.Total", inALibrary);
            data.Add("a record's member through a parameter", "StatelessComponent",
                "    private static string Read(Secret secret) => secret.V;", "Secret.V", inALibrary);
            data.Add("a struct's field through a parameter", "StatelessComponent",
                "    private static int Read(Amount amount) => amount.V;", "Amount.V", inALibrary);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ReadOffAValue))]
    public void AnInstanceMemberOfAKeptOutType_ReadOffAValue_IsRefusedOnceWhereThePageReadsIt(
        string what, string kind, string members, string member, bool inALibrary)
    {
        using var solution = Of(inALibrary, kind, members, "\"plain\"");
        var built = solution.Build();

        built.Failed.Should().BeTrue($"{what} names a member no twin holds");
        built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle($"{what} is one site:\n{built.Errors}")
            .Which.Should().StartWith(solution.FileOf("App", "Screens/Home.cs") + "(")
            .And.Contain("error EQ2010").And.Contain("never reaches the browser")
            .And.Contain($"'{(inALibrary ? "Common" : "App.Model")}.{member}'");
    }

    /// <summary>A type that crosses and hands out a value of a kept-out struct, written into the
    /// project that holds the server types.</summary>
    private static string WithShop(Solution solution, bool inALibrary)
    {
        if (inALibrary)
        {
            solution.Write("Common", "Shop.cs", ShopType.Replace("NS", "Common"));
            return solution.FileOf("Common", "Shop.cs");
        }
        solution.Write("App", "Model/Shop.cs", ShopType.Replace("NS", "App.Model"));
        return solution.FileOf("App", "Model/Shop.cs");
    }

    /// <summary>A kept-out type named only as a type argument of a base list, a component's or a
    /// class's, is written as nothing: a component's twin implements no interface and extends its base
    /// component without the argument, and a class's twin extends its base without it. So the header is free, as every other
    /// position the module writes as nothing is. The client-boundary spec listed the base list among the
    /// refused sites after #944, and the build never refused it; this pins what it does.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AKeptOutTypeArgumentOfABaseList_IsWrittenAsNothing(bool inALibrary)
    {
        using var component = Of(inALibrary, "StatelessComponent, IMark<Ledger>", "", "\"plain\"");
        component.Write("App", "Screens/IMark.cs", "namespace App.Screens;\n\npublic interface IMark<T>\n{\n}\n");
        var builtComponent = component.Build();

        builtComponent.Failed.Should().BeFalse($"a component's interfaces never reach its twin:\n{builtComponent.Errors}");
        builtComponent.Module("Home").Should().NotMatchRegex(@"(?<![\w$'])Ledger(?![\w$'])");

        using var page = Of(inALibrary, "Shell<Ledger>", "", "\"plain\"");
        page.Write("App", "Screens/Shell.cs",
            "using eQuantic.UI.Primitives;\n\nnamespace App.Screens;\n\npublic abstract class Shell<T> : StatelessComponent\n{\n}\n");
        var builtPage = page.Build();

        builtPage.Failed.Should().BeFalse($"a component's twin extends its base component without the argument:\n{builtPage.Errors}");
        builtPage.Module("Home").Should().Contain("extends Shell").And.NotMatchRegex(@"(?<![\w$'])Ledger(?![\w$'])");

        var space = inALibrary ? "Common" : "App.Model";
        using var holder = Of(inALibrary, "StatelessComponent", "", "new LedgerHolder().N.ToString()");
        holder.Write("App", "Screens/Holder.cs", "namespace App.Screens;\n\npublic class Holder<T>\n{\n    public int N => 1;\n}\n");
        holder.Write("App", "Screens/LedgerHolder.cs",
            $"using {space};\n\nnamespace App.Screens;\n\npublic sealed class LedgerHolder : Holder<Ledger>\n{{\n}}\n");
        var builtHolder = holder.Build();

        builtHolder.Failed.Should().BeFalse($"a class's twin extends its base without the type argument:\n{builtHolder.Errors}");
        builtHolder.Module("LedgerHolder").Should().Contain("extends Holder")
            .And.NotMatchRegex(@"(?<![\w$'])Ledger(?![\w$'])");
    }

    /// <summary>An operator of a kept-out struct, on a value a type that crosses hands out, names the
    /// struct where the page writes it. In the app it built, and the page called <c>Amount.opAdd</c> on
    /// a class nothing wrote; from a referenced project it was told twice, at the operator and at the
    /// member read off its answer.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOperatorOfAKeptOutStruct_IsRefusedWhereThePageWritesIt(bool inALibrary)
    {
        using var solution = Of(inALibrary, "StatelessComponent", "", "(Shop.Price() + Shop.Price()).V.ToString()");
        WithShop(solution, inALibrary);
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith(solution.FileOf("App", "Screens/Home.cs") + "("))
            .Should().ContainSingle(built.Errors).Which.Should().Contain("error EQ2010").And.Contain("'" + (inALibrary ? "Common" : "App.Model") + ".Amount'");
    }

    /// <summary>A compound through a kept-out struct's operator is told once, at the compound: the
    /// compound asked the operator's lowering twice, once to learn whether a twin carries it and once for
    /// the call it writes (#948's review).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACompoundThroughAKeptOutStructsOperator_IsToldOnce(bool inALibrary)
    {
        using var solution = Of(inALibrary, "StatelessComponent",
            "    private static string Touch()\n    {\n        var total = Shop.Price();\n        total += Shop.Price();\n        return \"touched\";\n    }",
            "Touch()");
        WithShop(solution, inALibrary);
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith(solution.FileOf("App", "Screens/Home.cs") + "("))
            .Should().ContainSingle(built.Errors)
            .Which.Should().Contain("error EQ2010").And.Contain("'" + (inALibrary ? "Common" : "App.Model") + ".Amount'");
    }

    /// <summary>The zero of a kept-out struct has no twin to build it, so the type that crosses and
    /// writes one is refused where it does: <c>Shop.Price()</c> answered undefined where C# answers a
    /// zeroed <c>Amount</c>. The page only calls it, so the zero is the one site; a page that reads a
    /// member off the value is a site of its own (<see cref="AMemberReadOffTheKeptOutValueAMethodAnswers_IsToldAtThePage"/>).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AZeroOfAKeptOutStruct_IsRefusedWhereTheTypeThatCrossesWritesIt(bool inALibrary)
    {
        using var solution = Of(inALibrary, "StatelessComponent",
            "    private static string Touch()\n    {\n        Shop.Price();\n        return \"touched\";\n    }", "Touch()");
        var shop = WithShop(solution, inALibrary);
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle(built.Errors)
            .Which.Should().StartWith(shop + "(").And.Contain("error EQ2010");
    }

    /// <summary>A member read off the value a method of a type that crosses answers is a member no twin
    /// holds, whatever the method answers: <c>Shop.Price().V</c> threw in the browser, and is told where
    /// the page reads it, once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMemberReadOffTheKeptOutValueAMethodAnswers_IsToldAtThePage(bool inALibrary)
    {
        using var solution = Of(inALibrary, "StatelessComponent", "", "Shop.Price().V.ToString()");
        WithShop(solution, inALibrary);
        var built = solution.Build();

        built.Failed.Should().BeTrue();
        built.Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith(solution.FileOf("App", "Screens/Home.cs") + "("))
            .Should().ContainSingle(built.Errors)
            .Which.Should().Contain("error EQ2010").And.Contain($"'{(inALibrary ? "Common" : "App.Model")}.Amount.V'");
    }

    /// <summary>A position the module writes as nothing names no twin, so it is free: a component's
    /// field and parameter are annotated <c>any</c>, a cast is the value, a list of the type holds
    /// references, its zero is null, and <c>nameof</c> is text.</summary>
    public static TheoryData<string, string, string, bool> Unnamed()
    {
        var data = new TheoryData<string, string, string, bool>();
        foreach (var inALibrary in new[] { false, true })
        {
            data.Add("a field", "    private Ledger? _ledger;", "_ledger is null ? \"none\" : \"some\"", inALibrary);
            data.Add("a parameter", "    private static string Of(Ledger? ledger) => ledger is null ? \"none\" : \"some\";", "Of(null)", inALibrary);
            data.Add("a list and its default", "    private readonly List<Ledger> _all = new();", "_all.FirstOrDefault() is null ? \"none\" : \"some\"", inALibrary);
            data.Add("a Server Action's dictionary parameter", "    [ServerAction]\n    public Task<int> Save(Dictionary<string, Ledger> map) => Task.FromResult(map.Count);", "\"plain\"", inALibrary);
            data.Add("a cast", "    private static object Cast(object o) => (Ledger)o;", "Cast(\"o\").ToString() ?? \"\"", inALibrary);
            data.Add("a nameof", "", "nameof(Ledger) + nameof(Ledger.Total)", inALibrary);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Unnamed))]
    public void APositionTheModuleWritesAsNothing_IsFree(string what, string members, string shown, bool inALibrary)
    {
        using var solution = Of(inALibrary, "StatefulComponent", members, $"({shown}) + new App.Model.Badge(\"b\").Cast(\"x\")");
        solution.Write("App", "Model/Badge.cs",
            $"using {(inALibrary ? "Common" : "App.Model")};\n\nnamespace App.Model;\n\npublic sealed record Badge(string Label)\n{{\n    public object Cast(object x) => (Ledger)x;\n    public string Kind => \"Ledger\" + nameof(Ledger) + nameof(Vault.Key) + Label;\n}}\n");
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{what} names no twin:\n{built.Errors}");
        built.Module("Home").Should().NotMatchRegex(@"(?<![\w$'])Ledger(?![\w$'])", $"{what} is written as nothing of Ledger's");
        built.Module("Badge").Should().Contain("'Ledger'").And.NotContain("import { Ledger }");
    }

    /// <summary>Code that never crosses names the type freely: a Server Action's body, a
    /// <c>[ServerOnly]</c> member of a page and of a class that crosses, and another
    /// <c>[ServerOnly]</c> class.</summary>
    [Theory]
    [InlineData("a Server Action's body", "    [ServerAction]\n    public Task<string> Load() => Task.FromResult(Vault.Key() + new Ledger().Total + \"x\".Sealed());", "", false)]
    [InlineData("a Server Action's body", "    [ServerAction]\n    public Task<string> Load() => Task.FromResult(Vault.Key() + new Ledger().Total + \"x\".Sealed());", "", true)]
    [InlineData("a [ServerOnly] member", "    [ServerOnly]\n    public string Warm()\n    {\n        var ledger = new Ledger();\n        return Vault.Key() + ledger.Total + default(Amount).V;\n    }", "", false)]
    [InlineData("a [ServerOnly] member", "    [ServerOnly]\n    public string Warm()\n    {\n        var ledger = new Ledger();\n        return Vault.Key() + ledger.Total + default(Amount).V;\n    }", "", true)]
    [InlineData("a [ServerOnly] member of a class that crosses", "", "public sealed class Keys\n{\n    public int Size => 1;\n\n    [ServerOnly]\n    public string All() => Vault.Key() + new Ledger().Total;\n}\n", false)]
    [InlineData("another [ServerOnly] class", "", "[ServerOnly]\npublic static class Keys\n{\n    public static string All() => Vault.Key() + new Ledger().Total;\n}\n", false)]
    public void ServerCodeNamesAKeptOutTypeFreely(string what, string members, string otherType, bool inALibrary)
    {
        using var solution = Of(inALibrary, "StatefulComponent", members, "\"plain\"");
        if (otherType.Length > 0)
            solution.Write("App", "Model/Keys.cs", "using eQuantic.UI.Primitives;\n\nnamespace App.Model;\n\n" + otherType);
        var built = solution.Build();

        built.Failed.Should().BeFalse($"{what} never crosses:\n{built.Errors}");
        foreach (var module in built.Modules)
            built.Module(module).Should().NotMatchRegex(@"(?<![\w$'])(Vault|Ledger)(?![\w$'])", $"{what} is in no module, {module}'s included");
    }

    /// <summary>A component crosses whatever it is marked, as <c>[ServerOnly]</c>'s own doc says: the
    /// parser writes its module, so a page that places it names a twin that exists.</summary>
    [Fact]
    public void AComponentMarkedServerOnly_StillCrossesAndIsPlaced()
    {
        using var solution = new Solution().App("App", globalUsings: SharedLibrary.Factories, files:
        [
            ("Screens/Badge.cs", "using eQuantic.UI.Primitives;\n\nnamespace App.Screens;\n\n[ServerOnly]\npublic sealed class Badge : StatelessComponent\n{\n    public override VisualNode Build(ComponentContext context) =>\n        Text(\"b\", TypeRole.BodyM, context.Theme.TextPrimary, maxLines: 1);\n}\n"),
            ("Screens/Home.cs", "using eQuantic.UI.Primitives;\n\nnamespace App.Screens;\n\npublic sealed class Home : StatelessComponent\n{\n    public override VisualNode Build(ComponentContext context) => new Badge();\n}\n"),
        ]);
        var built = solution.Build();

        built.Failed.Should().BeFalse(built.Errors);
        built.Module("Badge").Should().Contain("export class Badge");
        built.SiblingImportsOf("Home").Should().Contain("Badge");
    }
}
