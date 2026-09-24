using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// The culture a number is READ in. The browser reads the invariant culture and no other: the
/// runtime has no parser for another culture's text, and <c>CultureInfo</c> has no twin. So a
/// provider that names the invariant culture is left out and the reading is exact. No provider at
/// all is EQ2110, because C# then reads in the request's culture (<c>"1,5"</c> is 1.5 under pt)
/// while the browser reads 15. Any other provider, the current culture, a variable or a call, is
/// EQ2108. The culture is recognised as <c>ToString</c> recognises it (<see cref="NamedCulture"/>):
/// by the property the provider binds to, since a value that only exists at run time cannot be
/// honoured at build time.
/// </summary>
internal static class ParseCulture
{
    /// <summary>Reports what <paramref name="provider"/>, or its absence, means for a number read in
    /// the browser. A null literal is no provider, as it is in .NET.</summary>
    public static void Check(SyntaxNode call, ExpressionSyntax? provider, ConversionContext context)
    {
        if (provider is null or LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression })
        {
            context.Report(call, ConversionSeverity.Warning, "EQ2110",
                "A number read from text with no culture reads differently on each target: C# reads "
                + "in the request's culture (\"1,5\" is 1.5 in pt) and the browser always in the "
                + "invariant one. Say which you mean: pass CultureInfo.InvariantCulture.");
            return;
        }
        if (NamedCulture.IsInvariant(provider, context)) return;
        context.Report(call, ConversionSeverity.Error, "EQ2108",
            "Only CultureInfo.InvariantCulture crosses for reading a number: the browser has no "
            + "parser for another culture's text. Read it with CultureInfo.InvariantCulture.");
    }
}
