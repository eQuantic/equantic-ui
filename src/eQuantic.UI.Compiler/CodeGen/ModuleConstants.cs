using System.Globalization;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The constants one module declares after its imports (#547): what its code reads at every call and
/// is built once, an enum's table. A conversion asks for it by the symbol it belongs to and writes the
/// name it gets back, and the first ask declares it. An enum's table was an object literal written at
/// every call, built every time the expression ran: a list that printed a status for a thousand rows
/// built a thousand of them per render.
/// <para>
/// A name is the symbol's own behind a dollar, which no C# name can carry, with its first letter
/// upper-cased, so it never meets one of the lowercase names the lowering binds (<c>$eq</c>,
/// <c>$r</c>, <c>$v</c>). A second symbol of the same name in the same module takes a number after it.
/// </para>
/// </summary>
public sealed class ModuleConstants
{
    private readonly Dictionary<ISymbol, string> _names = new(SymbolEqualityComparer.Default);
    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);
    private readonly List<JsConstant> _declared = new();

    /// <summary>A module's, opened by <see cref="CSharpToJsConverter.BeginModule"/> and nowhere else.</summary>
    internal ModuleConstants()
    {
    }

    /// <summary>The declarations, in the order they were first asked for.</summary>
    internal IReadOnlyList<JsConstant> Declared => _declared;

    /// <summary>
    /// The name of the constant <paramref name="owner"/>'s value is held in, declared with
    /// <paramref name="value"/> the first time it is asked for.
    /// </summary>
    internal string NameOf(ISymbol owner, Func<string> value)
    {
        if (_names.TryGetValue(owner, out var name)) return name;
        var stem = "$" + (owner.Name.Length == 0 ? "_" : char.ToUpperInvariant(owner.Name[0]) + owner.Name[1..]);
        name = stem;
        for (var suffix = 2; !_taken.Add(name); suffix++)
            name = stem + suffix.ToString(CultureInfo.InvariantCulture);
        _names[owner] = name;
        _declared.Add(new JsConstant(name, value()));
        return name;
    }
}
