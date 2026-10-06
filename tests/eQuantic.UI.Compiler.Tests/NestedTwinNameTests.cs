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
}
