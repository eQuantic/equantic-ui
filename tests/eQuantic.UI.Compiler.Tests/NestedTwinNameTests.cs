using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type's twin and its module are named by the types that contain it and its own name, joined by
/// <c>$</c> (#584), generic arguments erased, and a nested type of an owner that never crosses has no
/// module at all.
/// </summary>
public class NestedTwinNameTests
{
    [Fact]
    public void ATypesModule_IsNamedByItsOwnersAndItsOwnName()
    {
        var results = new ComponentCompiler().CompileSource("""
            public class Top { }
            public class Cart { public class Item { public int Qty = 9; } }
            public class A { public class B { public class C { public int V = 1; } } }
            public class Box<T> { public record Node(T Value); }
            """, "Probe.cs");

        results.Select(result => result.ComponentName).Should()
            .Contain(["Top", "Cart", "Cart$Item", "A", "A$B", "A$B$C", "Box", "Box$Node"])
            .And.NotContain(["Item", "B", "C", "Node"]);
        results.Single(result => result.ComponentName == "A$B$C").TypeScript.Should().Contain("export class A$B$C");
    }

    /// <summary>A top-level type named by a word JavaScript reserves, or by a global the emitted code
    /// reads, is its name and a <c>$</c>, its module too, as a local of that name is (#934, #890): the
    /// module declared <c>export class function</c>, which nothing parses. A nested one is named by its
    /// owner already, and a type of .NET's keeps its name.</summary>
    [Fact]
    public void ATypeNamedByAReservedWordOrAGlobal_IsItsNameAndADollar()
    {
        var results = new ComponentCompiler().CompileSource("""
            public class function { }
            public class @class { }
            public static class Math { public static int One() => 1; }
            public class Outer { public class function { } }
            """, "Probe.cs");

        results.Select(result => result.ComponentName).Should()
            .Contain(["function$", "class$", "Math$", "Outer", "Outer$function"])
            .And.NotContain(["function", "class", "Math", "Outer$function$"]);
        results.Single(result => result.ComponentName == "function$").TypeScript.Should().Contain("export class function$ ");
    }

    /// <summary>Compiled alone, as the playground compiles, a component that names a class of its own
    /// file named like a reserved word imports no twin of it from the runtime: the file's own types were
    /// known by their C# names, and <c>function$</c> was taken for the vocabulary's (found by the review
    /// of #934).</summary>
    [Fact]
    public void AloneAComponentNamingARenamedClassOfItsFile_ImportsItFromNoRuntime()
    {
        var page = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Primitives;
            public class function { public static string S = "x"; }
            public sealed class Page : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text(function.S, TypeRole.BodyM);
            }
            """, "Probe.cs").Single(result => result.ComponentName == "Page");

        page.TypeScript.Should().Contain("function$.s")
            .And.NotMatchRegex(@"import \{[^}]*function\$[^}]*\} from ""@equantic/runtime""");
    }

    /// <summary>A class of the app's named <c>Math</c> is imported and called as its twin, so .NET's
    /// <c>Math</c> beside it is the global's (#890): the module imported the class as <c>Math</c>, and
    /// <c>System.Math.Max</c> called the class.</summary>
    [Fact]
    public void AClassNamedLikeAGlobal_IsImportedByItsTwin_BesideTheGlobal()
    {
        var calc = CompileScanned("""
            public static class Math { public static int Twice(int x) => x * 2; }
            public class Calc { public static int Run() => Math.Twice(2) + System.Math.Max(1, 2); }
            """, "Calc");
        calc.Errors.Should().BeEmpty();
        calc.TypeScript.Should().Contain("Math$.twice(2) + Math.max(1, 2)")
            .And.MatchRegex(@"import \{[^}]*(?<![\w$])Math\$(?![\w$])[^}]*\} from ""\./Math\$""")
            .And.NotMatchRegex(@"import \{[^}]*(?<![\w$])Math(?![\w$])[^}]*\}");
    }

    /// <summary>A component's action is called by the component's twin, the name the server registers it
    /// under (<c>TwinName.OfType</c>): <c>Map$</c> for a component named like a global, and the owner's
    /// name first for a nested one.</summary>
    [Fact]
    public void AnActionsComponent_IsNamedByItsTwin()
    {
        var results = new ComponentCompiler().CompileSource("""
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;
            public sealed class Map : StatelessComponent
            {
                [ServerAction] public Task<int> Pins() => Task.FromResult(2);
                public override VisualNode Build(ComponentContext context) => new Text("map", TypeRole.BodyM);
            }
            public static class Host
            {
                public sealed class Page : StatelessComponent
                {
                    [ServerAction] public Task<int> Hits() => Task.FromResult(7);
                    public override VisualNode Build(ComponentContext context) => new Text("page", TypeRole.BodyM);
                }
            }
            """, "Probe.cs");

        results.Single(result => result.ComponentName == "Map$").TypeScript.Should().Contain("invoke('Map$/Pins', [])");
        results.Single(result => result.ComponentName == "Host$Page").TypeScript.Should().Contain("invoke('Host$Page/Hits', [])");
    }

    [Fact]
    public void ANestedTypeOfAServerOnlyOwner_HasNoModule()
    {
        var results = new ComponentCompiler().CompileSource("""
            [eQuantic.UI.Primitives.ServerOnly] public class Vault { public class Key { public int Id = 1; } public record Seal(int N); }
            public class Shelf { public class Box { public int Size = 2; } }
            """, "Probe.cs");

        results.Select(result => result.ComponentName).Should().Contain("Shelf$Box")
            .And.NotContain(name => name.StartsWith("Vault"), "a server-only owner's nested types never cross, as it never does");
    }

    /// <summary>A client reference to a nested type whose owner never crosses names a twin nothing wrote:
    /// the build refuses it (EQ2010), a construction and a type test alike (found by Copilot's review of
    /// #654), where the page met `Vault$Key is not defined` at load.</summary>
    [Fact]
    public void AReferenceToANestedTypeOfAServerOnlyOwner_IsRefused()
    {
        var results = new ComponentCompiler().CompileSource("""
            [eQuantic.UI.Primitives.ServerOnly] public class Vault { public class Key { public int Id = 1; } }
            public class Client
            {
                public int Make() => new Vault.Key().Id;
                public bool Is(object o) => o is Vault.Key;
            }
            """, "Probe.cs");
        var client = results.Single(result => result.ComponentName == "Client");
        client.Errors.Select(error => error.Code).Should().Contain("EQ2010");
        client.Errors.Count(error => error.Code == "EQ2010").Should().BeGreaterThanOrEqualTo(2, "the construction and the type test each name it");
    }

    /// <summary>A nested type inside an array or a generic is annotated by its twin's name, as a bare one
    /// is, in a class and in a record alike, where the text named `Inner`, which nothing declares (found
    /// by Copilot's review of #654). Two owners' `Inner` in one annotation name two twins, and a
    /// top-level `Inner` beside a nested one keeps its own name.</summary>
    [Fact]
    public void ANestedTypeInsideAnArrayOrAGeneric_IsAnnotatedByItsTwin()
    {
        var results = new ComponentCompiler().CompileSource("""
            using System.Collections.Generic;
            public class Inner { }
            public class A { public class Inner { } }
            public class B { public class Inner { } }
            public class Holder
            {
                public A.Inner[] Items = new A.Inner[0];
                public List<A.Inner> More = new();
                public System.Func<A.Inner, B.Inner> Pairs = null;
                public (Inner, B.Inner) Mixed = default;
            }
            public record Kept(A.Inner One, A.Inner[] Many, System.Func<A.Inner, B.Inner> Pairs);
            """, "Probe.cs");
        var holder = results.Single(result => result.ComponentName == "Holder").TypeScript;
        var kept = results.Single(result => result.ComponentName == "Kept").TypeScript;
        Annotation(holder, "items").Should().Be("A$Inner[]");
        Annotation(holder, "more").Should().Be("A$Inner[]");
        Annotation(holder, "pairs").Should().MatchRegex(@"A\$Inner\b.*B\$Inner\b").And.NotMatchRegex(@"(?<![\w$])Inner\b");
        Annotation(holder, "mixed").Should().MatchRegex(@"(?<![\w$])Inner\b.*B\$Inner\b");
        kept.Should().MatchRegex(@"\bone\??: A\$Inner\b").And.MatchRegex(@"\bmany\??: A\$Inner\[\]")
            .And.NotMatchRegex(@"[:<,(] ?Inner\b");
    }

    /// <summary>An empty list of a nested class, and of tuples holding one, is annotated by the twin's name and
    /// imports it, as any reference to it does (Copilot on #1068): the list and the tuple's slot named
    /// <c>Item</c>, which nothing declares.</summary>
    [Fact]
    public void AnEmptyListOfANestedClass_IsAnnotatedByItsTwin_AndImportsIt()
    {
        var basket = CompileScanned("""
            using System.Collections.Generic;
            public class Cart { public class Item { public int Qty = 9; } }
            public class Basket
            {
                public int Count()
                {
                    var entries = new List<(Cart.Item Entry, int Index)>();
                    var items = new List<Cart.Item>();
                    return entries.Count + items.Count;
                }
            }
            """, "Basket");
        basket.Errors.Should().BeEmpty();
        basket.TypeScript.Should().Contain("let entries: [Cart$Item, number][] = [];")
            .And.Contain("let items: Cart$Item[] = [];")
            .And.MatchRegex(@"import \{[^}]*(?<![\w$])Cart\$Item(?![\w$])[^}]*\} from ""\./Cart\$Item""");
    }

    private static string Annotation(string typeScript, string field) =>
        System.Text.RegularExpressions.Regex.Match(typeScript, $@"\b{field}!: (.+);").Groups[1].Value;

    private const string KeptOutPrelude = """
        [eQuantic.UI.Primitives.ServerOnly] public class Vault
        {
            public class Key { public static int Count = 1; public static int Make() => 2; }
            public struct Pair { public int A; }
            public struct Amount
            {
                public int V;
                public static Amount operator +(Amount a, Amount b) => new Amount { V = a.V + b.V };
                public static implicit operator int(Amount a) => a.V;
            }
        }
        public class Shop { public static Vault.Amount Price() => default; }
        """;

    /// <summary>Every way client code reaches a nested type whose owner never crosses is refused with
    /// EQ2010 (found by Copilot's third review of #654): an alias, a member or a call through using
    /// static, a zero an expression asks for or an emitter writes for a field, and an operator or a
    /// conversion the value's type brings. None of them names the type where the first fences looked,
    /// and each emitted a twin nothing writes.</summary>
    [Theory]
    [InlineData("using K = Vault.Key;", "public int Read() => K.Count;")]
    [InlineData("using static Vault.Key;", "public int Read() => Count;")]
    [InlineData("using static Vault.Key;", "public int Read() => Make();")]
    [InlineData("", "public object Read() => default(Vault.Pair);")]
    [InlineData("", "public Vault.Pair Held;")]
    [InlineData("", "public int Read() => (Shop.Price() + Shop.Price()).V;")]
    [InlineData("", "public int Read() => Shop.Price();")]
    [InlineData("", "public Vault.Key Kept = null;")]   // no fence sees it: the module's import net does
    public void EveryWayToReachAKeptOutNestedType_IsRefused(string usings, string member)
    {
        var client = CompileScanned(usings + "\n" + KeptOutPrelude + "\npublic class Client { " + member + " }", "Client");
        client.Errors.Select(error => error.Code).Should().Contain("EQ2010");
        client.TypeScript.Should().NotContain("from \"./Vault$", "no module of Vault's is written");
    }

    /// <summary>A record's member of a nested type whose owner never crosses is refused where it is
    /// declared, as a class's is: the record's zero for it named a twin nothing writes.</summary>
    [Fact]
    public void ARecordMemberOfAKeptOutNestedType_IsRefused()
    {
        var record = CompileScanned(KeptOutPrelude + "\npublic record Client(Vault.Key K, int N);", "Client");
        record.Errors.Select(error => error.Code).Should().Contain("EQ2010");
    }

    /// <summary>A nested type of an owner the runtime provides is the runtime's, as the parser reads it:
    /// imported from the runtime under its twin name, the way the runtime carries
    /// <c>CodeBlock$CodeMetrics</c>. It named a sibling module nothing writes (found by Copilot's third
    /// review of #654).</summary>
    [Fact]
    public void ANestedTypeOfARuntimeProvidedOwner_IsTheRuntimes()
    {
        var client = CompileScanned("""
            [eQuantic.UI.Primitives.RuntimeProvided] public class Kit { public class Part { public int N = 1; } }
            public class Client { public int Make() => new Kit.Part().N; }
            """, "Client");
        client.Errors.Should().BeEmpty();
        client.TypeScript.Should().MatchRegex(@"import \{[^}]*(?<![\w$])Kit\$Part(?![\w$])[^}]*\} from ""@equantic/runtime""")
            .And.NotContain("./Kit$Part");
    }

    /// <summary>The same through <c>using static</c> and a bare call: the class the call introduces is
    /// the runtime's too. The import's bucket was read from the namespace alone, so an owner the
    /// runtime provides from an app's namespace sent <c>Kit$Part</c> to a module of the app's, which
    /// nothing writes (found by Copilot's third review of #708).</summary>
    [Fact]
    public void AStaticOfARuntimeProvidedOwnersNestedType_ReachedBare_IsTheRuntimes()
    {
        var client = CompileScanned("""
            using static Kit.Part;
            [eQuantic.UI.Primitives.RuntimeProvided] public class Kit { public static class Part { public static int Make() => 1; } }
            public class Client { public int Use() => Make(); }
            """, "Client");
        client.Errors.Should().BeEmpty();
        client.TypeScript.Should().MatchRegex(@"import \{[^}]*(?<![\w$])Kit\$Part(?![\w$])[^}]*\} from ""@equantic/runtime""")
            .And.NotContain("./Kit$Part");
    }

    /// <summary>A file compiled as eqc compiles a project's: scanned by the dependency resolver first,
    /// whose module set decides every import a module writes, then compiled with it. A compile with no
    /// scan imports none of the app's modules, so it cannot show one imported, or refused.</summary>
    private static CompilationResult CompileScanned(string source, string component)
    {
        var dir = Directory.CreateTempSubdirectory("eq-nested-").FullName;
        try
        {
            var path = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(path, source);
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler { SymbolsAreAuthoritative = false };
            compiler.SetDependencyResolver(resolver);
            return compiler.CompileFile(path).Single(result => result.ComponentName == component);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

