using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The one path from a symbol to the syntax that declares it, which notes the file it reads in the
/// record of the compile in progress (<see cref="SourceReads"/>, #1281). A served eqc decides from those
/// notes which files a save that changed bodies alone can have changed, so a read that does not pass
/// here is a module left stale, which builds green and is wrong in the browser: the guard
/// (<c>SyntaxReadsTests</c>) fails on one.
/// <para>
/// Whether a symbol is declared in source, and how many declarations it has, say nothing a body holds,
/// and read nothing: <c>DeclaringSyntaxReferences.Length</c> and
/// <c>Locations.Any(location =&gt; location.IsInSource)</c> stay as they are.
/// </para>
/// </summary>
internal static class SymbolSources
{
    /// <summary>The syntax of <paramref name="symbol"/>'s declarations, each file noted as read.</summary>
    internal static ImmutableArray<SyntaxReference> ReadDeclarations(this ISymbol symbol)
    {
        var references = symbol.DeclaringSyntaxReferences;
        foreach (var reference in references) SourceReads.Note(reference.SyntaxTree);
        return references;
    }

    /// <summary>Where <paramref name="symbol"/> is declared, each file noted as read: a position is a
    /// read, since a body above it moves it.</summary>
    internal static ImmutableArray<Location> ReadLocations(this ISymbol symbol)
    {
        var locations = symbol.Locations;
        foreach (var location in locations) SourceReads.Note(location.SourceTree);
        return locations;
    }

    /// <summary>The syntax of the attribute as it is applied, its file noted as read.</summary>
    internal static SyntaxReference? ReadApplication(this AttributeData attribute)
    {
        var reference = attribute.ApplicationSyntaxReference;
        SourceReads.Note(reference?.SyntaxTree);
        return reference;
    }
}
