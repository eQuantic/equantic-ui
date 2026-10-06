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
}
