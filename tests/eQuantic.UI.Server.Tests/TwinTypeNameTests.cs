using FluentAssertions;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The server names the page it serves by the CLR type, and eqc names the module it writes from the
/// symbol: one rule, <c>TwinName.OfType</c>, its declaring types' names and its own joined by <c>$</c>,
/// generic arity erased (#584). The compiler's twin of these cases is <c>NestedTwinNameTests</c>
/// (<c>Cart$Item</c>, <c>A$B$C</c>, <c>Box$Node</c>). A page declared inside a class was asked for by its
/// simple name, a module nobody writes.
/// </summary>
public class TwinTypeNameTests
{
    public class Shelf
    {
        public class Box { }

        public class Crate<T>
        {
            public class Slot { }
        }
    }

    [Fact]
    public void AType_IsNamedByTheTypesItIsNestedIn_WithNoGenericArity()
    {
        TwinName.OfType(typeof(TwinTypeNameTests)).Should().Be("TwinTypeNameTests");
        TwinName.OfType(typeof(Shelf.Box)).Should().Be("TwinTypeNameTests$Shelf$Box");
        TwinName.OfType(typeof(Shelf.Crate<int>)).Should().Be("TwinTypeNameTests$Shelf$Crate");
        TwinName.OfType(typeof(Shelf.Crate<int>.Slot)).Should().Be("TwinTypeNameTests$Shelf$Crate$Slot");
    }

    [eQuantic.UI.Primitives.ServerOnly]
    public class Vault { public class Key { } }

    /// <summary>A type declared inside one that never crosses, a [ServerOnly] owner here, has no twin, so
    /// the server publishes no route and maps no page for it (found by Copilot's review of #654).</summary>
    [Fact]
    public void ATypeInsideAServerOnlyOwner_DoesNotCross()
    {
        TwinName.OwnersCross(typeof(Vault.Key)).Should().BeFalse();
        TwinName.OwnersCross(typeof(Shelf.Box)).Should().BeTrue();
        TwinName.OwnersCross(typeof(Shelf.Crate<int>.Slot)).Should().BeTrue();
    }

    /// <summary>The client's route table names a page inside an owner that crosses by its twin, and has
    /// no route to one inside a [ServerOnly] owner, whose module eqc never writes (found by Copilot's
    /// review of #654), where navigating to it asked for <c>ServerVault$Door</c>.</summary>
    [Fact]
    public void APageInsideAServerOnlyOwner_IsNoRoute()
    {
        var options = new UIOptions();
        options.ScanAssembly(typeof(TwinTypeNameTests).Assembly);

        var routes = eQuantic.UI.Server.Client.AppSurface.Of(options, pattern => [pattern]).Routes;

        routes.Should().ContainSingle(route => route.Pattern == "/kept-out/open").Which.Page.Should().Be("OpenShelf$Gate");
        routes.Should().NotContain(route => route.Pattern == "/kept-out/vault");
    }
}
