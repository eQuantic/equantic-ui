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
/// A member a property pattern reads is held the same way, where reading it runs the app's code: .NET's
/// decision graph fetches each member of each value once for the operation, however many alternatives,
/// conjuncts, bindings, arms or sections read it, so <c>c is { Corner: { X: 2 } or { Y: 2 } }</c> calls
/// the <c>Corner</c> getter once, where reading it again at each mention called it four times (#1150).
/// </para>
/// <para>
/// A temporary is named by the operation and kept for each value and <c>Deconstruct</c> or member it is
/// asked for. The pattern that first reads it there assigns it (<c>$p12_0 ?? ($p12_0 = t.deconstruct())</c>,
/// never null, since every method with outs answers an object), and every later one reads it. The
/// operation declares its temporaries itself, where each evaluation of it starts them over: the switch
/// expression's arrow, the switch statement's block, and an arrow of the <c>is</c>'s own. So none of it
/// asks for a statement around it, and an initializer is held as any method body is.
/// </para>
/// </summary>
internal sealed class MatchParts
{
    private sealed class Operation(int at)
    {
        public int At { get; } = at;
        public List<(string Access, ISymbol Read, string Held)> Held { get; } = [];
        public List<string> Names { get; } = [];
    }

    private readonly List<Operation> _operations = [];

    /// <summary><paramref name="convert"/> run as the operation that starts at <paramref name="at"/>: its
    /// value, and the temporaries it holds parts and members in, which the operation declares.</summary>
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
    /// The temporary the operation being converted holds what <paramref name="read"/> answers for the value
    /// at <paramref name="access"/> in, a <c>Deconstruct</c>'s parts or a member's value: the same one for
    /// every pattern of the operation that reads that value through it.
    /// </summary>
    public string Held(string access, ISymbol read)
    {
        if (_operations.Count == 0)
            throw new InvalidOperationException(
                "A pattern holds what it reads inside a pattern-matching operation, which declares the temporary "
                + "it is held in: convert it through MatchParts.In.");
        var operation = _operations[^1];
        foreach (var (heldAccess, heldRead, name) in operation.Held)
            if (heldAccess == access && SymbolEqualityComparer.Default.Equals(heldRead, read)) return name;
        var held = $"$p{operation.At}_{operation.Names.Count}";
        operation.Held.Add((access, read, held));
        operation.Names.Add(held);
        return held;
    }
}
