using System.Text.RegularExpressions;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Each type declared in source whose twin's name a writer wrote into the module, once for every time it
/// wrote it, under that name (#830). A module's <c>./Order</c> is the module of the <c>Order</c> it
/// wrote, and the writer is the one that knows which: the app's own record, or a referenced project's
/// class of the same simple name. Mapped back from the name to every declaration the module's syntax
/// names under it, a cast of a null, a <c>default</c> and a delegate's type reached the referenced
/// project's <c>Order</c>, though none of them writes a reference to it, and the build compiled it for
/// the browser, where its server code was refused.
/// <para>
/// A writer that writes a name and records nothing leaves the name with more uses in the module than
/// writes on record, and the module's imports are then decided as they were before
/// (<see cref="Services.ReferencedNames"/>): a writer this does not know of costs precision, never a
/// type the module does write.
/// </para>
/// </summary>
internal sealed class WrittenTwins
{
    private readonly Dictionary<string, List<INamedTypeSymbol>> _written = new(StringComparer.Ordinal);

    /// <summary>The types written under <paramref name="name"/>, one entry for each write.</summary>
    public IReadOnlyList<INamedTypeSymbol> Under(string name) =>
        _written.TryGetValue(name, out var written) ? written : [];

    /// <summary>Records one write of <paramref name="type"/>'s twin, when it is a type of the source.</summary>
    public void Wrote(INamedTypeSymbol? type)
    {
        if (type?.OriginalDefinition is not { } definition || !definition.Locations.Any(location => location.IsInSource))
            return;
        var name = definition.TwinReference();
        if (!_written.TryGetValue(name, out var written)) _written[name] = written = [];
        written.Add(definition);
    }

    /// <summary>
    /// Records what <paramref name="written"/>, a name or an annotation a writer built as text, writes of
    /// <paramref name="type"/>: each type of the source within it (its elements, its type arguments, what
    /// it is nested in) once for each use of its twin's name the text holds.
    /// </summary>
    public void Wrote(ITypeSymbol? type, string? written)
    {
        if (type is null || string.IsNullOrEmpty(written)) return;
        var named = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Collect(type, named);
        foreach (var each in named)
            for (var use = Uses(written, each.TwinReference()); use > 0; use--)
                Wrote(each);
    }

    public void Clear() => _written.Clear();

    /// <summary>
    /// How many times <paramref name="code"/> uses <paramref name="name"/> as a name of its own: never as a
    /// member, after a dot. Everything else is counted, a string's text and a comment included, since a use
    /// counted that writes nothing only sends the module back to the imports it had before, where a use
    /// missed would let a write nobody recorded go uncounted. A spread (<c>...Order</c>) is a use.
    /// </summary>
    public static int Uses(string code, string name) =>
        Regex.Matches(QuotedNames.Replace(code, "''"), $@"(?<![\w$])(?<!(?<!\.\.)\.){Regex.Escape(name)}(?![\w$])").Count;

    /// <summary>
    /// A string literal that holds a name and nothing else (<c>'Order'</c>, <c>'App.Model.Order'</c>): a type's
    /// name as text, in a typeId or in an exception's chain, which is no use of the binding. Only such a literal
    /// is left out: in JavaScript a quote, a run of name characters and a quote can only be a literal, while a
    /// wider pattern could take the code between two literals for one, and miss a use.
    /// </summary>
    private static readonly Regex QuotedNames = new(@"'[\w.$]*'|""[\w.$]*""", RegexOptions.CultureInvariant);

    private static void Collect(ITypeSymbol type, HashSet<INamedTypeSymbol> named)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                Collect(array.ElementType, named);
                return;
            case INamedTypeSymbol each:
                foreach (var argument in each.TypeArguments) Collect(argument, named);
                if (each.ContainingType is { } owner) Collect(owner, named);
                if (each.OriginalDefinition.Locations.Any(location => location.IsInSource)) named.Add(each.OriginalDefinition);
                return;
        }
    }
}
