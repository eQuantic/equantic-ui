using Microsoft.CodeAnalysis;

namespace eQuantic.UI;

/// <summary>
/// The .NET shapes the hydration boundary knows how to carry, by their definition's namespace, name
/// and arity.
///
/// <para>
/// Two assemblies that cannot reference each other have to answer this identically. The source
/// generator lets a server value cross whole only as a shape the browser rebuilds, and eqc's spec is
/// what rebuilds it. While the generator answered from its own idea of a collection, a
/// <c>HashSet</c> crossed as the array the server's serializer writes, into a twin that calls
/// <c>.has</c> on a Set, and a <c>KeyValuePair</c>'s long arrived as the string no spec coerced.
/// </para>
/// </summary>
internal static class BoundaryShape
{
    private const string Generic = "System.Collections.Generic";

    /// <summary>
    /// A type of the platform, from <c>System</c> or <c>Microsoft</c>. eqc lowers such a type's members
    /// to the browser's own form of it (a list's <c>Count</c> is an array's <c>length</c>, a set's is a
    /// Set's <c>size</c>), never to a member of a plain copy.
    /// </summary>
    public static bool IsPlatform(ITypeSymbol? type)
    {
        var space = type?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return space == "System" || space.StartsWith("System.", System.StringComparison.Ordinal)
            || space == "Microsoft" || space.StartsWith("Microsoft.", System.StringComparison.Ordinal);
    }

    /// <summary>
    /// A sequence the browser holds as an array: a <c>List</c>, or one of the faces a list answers
    /// to. On a face a Set answers to as well (<c>ICollection</c>, <c>IEnumerable</c>), eqc lowers a
    /// read for either shape, so an array serves it.
    /// </summary>
    public static bool IsSequence(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeArguments.Length: 1 } named
        && named.OriginalDefinition.ContainingNamespace?.ToDisplayString() == Generic
        && named.OriginalDefinition.Name is "List" or "IList" or "IReadOnlyList"
            or "ICollection" or "IReadOnlyCollection" or "IEnumerable";

    /// <summary>
    /// The name of a dictionary the browser holds as a runtime class (<c>Dictionary</c>, or
    /// <c>SortedMap</c> for the sorted two), or null. Matched on name, namespace and arity rather than
    /// a display-string prefix, which <c>Dictionary&lt;,&gt;.KeyCollection</c> shares.
    /// </summary>
    public static string? DictionaryName(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { TypeArguments.Length: 2 } named) return null;
        var definition = named.OriginalDefinition;
        if (definition.ContainingNamespace?.ToDisplayString() != Generic) return null;
        return definition.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary" or "SortedDictionary" or "SortedList"
            ? definition.Name
            : null;
    }
}
