using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The temporaries a lowering binds in the function its C# is written in (#539): a value C#
/// evaluates once and reads again after a test, where what comes after the test must stay lazy.
/// The receiver of a null-conditional tail is the one, <c>(($n0 = receiver) == null ? null : …)</c>.
/// <para>
/// A function around the C# was the other way to bind it, and it is where two defects live: an
/// <c>await</c> in the tail lands in a function that is not <c>async</c>, so the module does not
/// parse, and an async one suspends where C# does not and adopts a task the call returned (#536).
/// A temporary is assigned where the C# runs, so an <c>await</c> in the tail is the enclosing
/// function's own, run only when C# runs it.
/// </para>
/// <para>
/// Whoever writes code a function runs DECLARES what was bound while converting it: the statement
/// dispatcher in front of each statement, and the lowering of a concise body (a member's, a local
/// function's, a lambda's) in its block. Every call of a function has its own slots, so an async
/// lambda run twice at once cannot overwrite the other call's receiver. Where nothing is open to
/// declare one (an initializer, which C# never lets await), <see cref="CanBind"/> is false and the
/// lowering keeps a way that needs no declaration.
/// </para>
/// </summary>
internal sealed class Temporaries
{
    private readonly List<List<string>> _scopes = [];
    private readonly Dictionary<SyntaxNode, IReadOnlyList<string>> _bound = [];
    private int _next;

    /// <summary>A name no other temporary of the module has. C# has no <c>$</c> in a name, so it is
    /// none of the app's own either.</summary>
    public string Fresh() => "$n" + _next++;

    /// <summary>Whether a statement or a concise body is open to declare a temporary.</summary>
    public bool CanBind => _scopes.Count > 0;

    /// <summary>Declares <paramref name="name"/> in front of the statement, or in the body, being
    /// written.</summary>
    public void Bind(string name)
    {
        var scope = _scopes[^1];
        if (!scope.Contains(name)) scope.Add(name);
    }

    /// <summary><paramref name="convert"/> run in a scope of its own: its value, and the temporaries
    /// it bound, which the caller declares.</summary>
    public (T Value, IReadOnlyList<string> Bound) In<T>(Func<T> convert)
    {
        var scope = new List<string>();
        _scopes.Add(scope);
        try
        {
            return (convert(), scope);
        }
        finally
        {
            _scopes.Remove(scope);
        }
    }

    /// <summary>How many temporaries the innermost scope holds, to tell after a conversion whether
    /// it bound one.</summary>
    public int Mark() => _scopes.Count == 0 ? 0 : _scopes[^1].Count;

    /// <summary>
    /// Records the temporaries converting <paramref name="node"/> bound since
    /// <paramref name="mark"/>: its translation names slots the statement it was converted in
    /// declares. A slot inside a lambda of its own is declared by that lambda, so it is not one of
    /// them.
    /// </summary>
    public void Remember(SyntaxNode node, int mark)
    {
        if (_scopes.Count > 0 && _scopes[^1].Count > mark) _bound[node] = _scopes[^1][mark..];
    }

    /// <summary>
    /// Whether the node cache may serve <paramref name="node"/>'s translation here: it names no
    /// temporary, or only ones the statement open now declares. Served in another statement, it
    /// would name a slot nothing there declares, so it is converted again instead and binds its own;
    /// served again in the same one, a fresh conversion would leave the first slot declared and
    /// never read, which TypeScript's <c>noUnusedLocals</c> refuses in a twin.
    /// </summary>
    public bool Serves(SyntaxNode node) =>
        !_bound.TryGetValue(node, out var names)
        || _scopes.Count > 0 && names.All(_scopes[^1].Contains);

    /// <summary>Drops what belonged to the previous emission, the names' count included, so a module
    /// numbers its own from <c>$n0</c>.</summary>
    public void Reset()
    {
        _scopes.Clear();
        _bound.Clear();
        _next = 0;
    }
}
