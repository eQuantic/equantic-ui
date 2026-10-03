using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// A member of .NET's own types reached bare, through <c>using static</c>, that no strategy claimed.
/// The class-static rule names a static member on its class, which is right for the types the
/// transpiler EMITS and names a class nothing defines for a platform one: <c>Double.naN</c> and
/// <c>String.join(",", ...parts)</c> compiled with no diagnostic (#485). One question, asked from
/// both branches that can return such a name, the member READ and the CALL, so the rule and its
/// message cannot drift apart.
/// </summary>
internal static class UsingStaticSymbolExtensions
{
    /// <summary>Reports EQ2004 and returns true when <paramref name="symbol"/> is a platform type's
    /// member, which has no translation once a strategy has passed it by.</summary>
    internal static bool ReportIfPlatformReachedBare(this ISymbol symbol, SyntaxNode node, ConversionContext context)
    {
        var declaring = symbol.ContainingType;
        if (declaring is null || !BoundaryShape.IsPlatform(declaring)) return false;
        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"'{declaring.ToDisplayString()}.{symbol.Name}', reached through `using static`, has no "
            + "JavaScript translation: nothing emits that class. Write it qualified where its "
            + "qualified form translates, or keep it on the server.");
        return true;
    }
}
