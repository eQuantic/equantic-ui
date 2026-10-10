using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Where one pattern-matching operation, an <c>is</c>, a switch statement or a switch expression, holds
/// what each <c>Deconstruct</c> the app wrote hands back, so the operation calls it ONCE for each value it
/// deconstructs, however many arms or alternatives read its parts. .NET calls it once per operation,
/// measured: <c>t switch { (1, _) =&gt; …, (var a, var b) =&gt; … }</c>, the same as a switch statement,
/// and <c>t is (1, _) or (_, 4)</c> call it once, and two separate <c>is</c> tests twice. A temporary per
/// pattern called it once per arm or alternative, and an initializer, where no statement can declare
/// one, called it once per part (Copilot's second review of #696), so a <c>Deconstruct</c> with an
/// effect answered differently and ran its effect again.
/// <para>
/// A temporary is named by the operation and kept for each value and <c>Deconstruct</c> it is asked for.
/// The pattern that first reads it there assigns it (<c>$p12_0 ?? ($p12_0 = t.deconstruct())</c>, never
/// null, since every method with outs answers an object), and every later one reads it. The operation
/// declares its temporaries itself, where each evaluation of it starts them over: the switch
/// expression's arrow, the switch statement's block, and an arrow of the <c>is</c>'s own. So none of it
/// asks for a statement around it, and an initializer is held as any method body is.
/// </para>
/// </summary>
internal sealed class MatchParts
{
    private sealed class Operation(int at)
    {
        public int At { get; } = at;
        public List<(string Access, IMethodSymbol Method, string Held)> Held { get; } = [];
        public List<string> Names { get; } = [];
    }

    private readonly List<Operation> _operations = [];

    /// <summary><paramref name="convert"/> run as the operation that starts at <paramref name="at"/>: its
    /// value, and the temporaries it holds parts in, which the operation declares.</summary>
    public (T Value, IReadOnlyList<string> Held) In<T>(int at, Func<T> convert)
    {
        var operation = new Operation(at);
        _operations.Add(operation);
        try
        {
            return (convert(), operation.Names);
        }
        finally
        {
            _operations.Remove(operation);
        }
    }

    /// <summary>
    /// The temporary the operation being converted holds the parts of the value at
    /// <paramref name="access"/> in, as <paramref name="deconstruct"/> hands them back: the same one for
    /// every pattern of the operation that deconstructs that value through it.
    /// </summary>
    public string Held(string access, IMethodSymbol deconstruct)
    {
        if (_operations.Count == 0)
            throw new InvalidOperationException(
                "A positional pattern calls a Deconstruct the app wrote inside a pattern-matching operation, which "
                + "declares the temporary its parts are held in: convert it through MatchParts.In.");
        var operation = _operations[^1];
        foreach (var (heldAccess, heldDeconstruct, name) in operation.Held)
            if (heldAccess == access && SymbolEqualityComparer.Default.Equals(heldDeconstruct, deconstruct)) return name;
        var held = $"$p{operation.At}_{operation.Names.Count}";
        operation.Held.Add((access, deconstruct, held));
        operation.Names.Add(held);
        return held;
    }
}
