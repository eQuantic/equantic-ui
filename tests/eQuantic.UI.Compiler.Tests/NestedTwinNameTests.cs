using eQuantic.UI.Compiler.CodeGen;
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

    private static string Annotation(string typeScript, string field) =>
        System.Text.RegularExpressions.Regex.Match(typeScript, $@"\b{field}!: (.+);").Groups[1].Value;
}
