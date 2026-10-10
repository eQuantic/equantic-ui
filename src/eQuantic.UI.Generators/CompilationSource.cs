using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Generators;

/// <summary>
/// The source a compilation HOLDS, which is what "declared in source" means to a generator. A
/// command-line build hands a referenced project over as metadata, with no syntax at all; the workspace
/// of an IDE or of <c>dotnet watch</c> hands it over as another compilation, whose syntax trees this one
/// does not contain. Taking "has syntax" for "is the app's own", the hydration manifest asked a semantic
/// model of a referenced project's tree, which throws, and failed on every hot reload of a page whose
/// base or whose callee lived in another project (CS8785, #627). Asked this way, the workspace reads the
/// same app the build reads.
/// </summary>
internal static class CompilationSource
{
    /// <summary>The declarations of <paramref name="symbol"/> that <paramref name="compilation"/> holds.</summary>
    public static IEnumerable<SyntaxReference> SourceIn(this ISymbol symbol, Compilation compilation) =>
        symbol.DeclaringSyntaxReferences.Where(reference => compilation.ContainsSyntaxTree(reference.SyntaxTree));

    /// <summary>Whether <paramref name="compilation"/> holds a declaration of <paramref name="symbol"/>.</summary>
    public static bool IsDeclaredIn(this ISymbol symbol, Compilation compilation) =>
        symbol.SourceIn(compilation).Any();
}
