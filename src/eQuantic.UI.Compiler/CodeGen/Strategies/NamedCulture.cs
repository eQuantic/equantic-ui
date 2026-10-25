using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Which culture a format provider NAMES, for the policies that read one: formatting
/// (<c>ToString</c>), and reading a number or a date from text and changing case, which take the
/// page's culture and the invariant one (<see cref="ReadsInvariant"/>).
/// Asked of the bound symbol, so only <c>System.Globalization.CultureInfo</c>'s own properties count:
/// a member of another type that happens to be called <c>InvariantCulture</c> may return any culture,
/// and its getter may have an effect that leaving the argument out would lose. Where the model cannot
/// be asked (no model, or a node a strategy rewrote), the author's spelling decides, and only the
/// exact one: <c>CultureInfo.InvariantCulture</c>, with or without its namespace.
/// <para>
/// A NULL provider names the current culture, as .NET reads it: <c>string.Format(null, …)</c> and
/// <c>x.ToString((IFormatProvider?)null)</c> format as the provider-less call does.
/// </para>
/// </summary>
internal static class NamedCulture
{
    public static bool IsInvariant(ExpressionSyntax provider, ConversionContext context) =>
        Names(provider, "InvariantCulture", context);

    public static bool IsCurrent(ExpressionSyntax provider, ConversionContext context) =>
        IsNull(provider, context) || Names(provider, "CurrentCulture", context);

    /// <summary>
    /// Whether a call that reads a culture reads the invariant one, where <paramref name="provider"/> is
    /// <c>CultureInfo.InvariantCulture</c>, or the page's, where it is none, the current culture or a
    /// null. Any other culture, one held in a variable included, has no counterpart in the browser,
    /// which knows those two: the build fails (EQ2108), naming what the call was <paramref name="doing"/>.
    /// </summary>
    public static bool ReadsInvariant(SyntaxNode call, ExpressionSyntax? provider, ConversionContext context, string doing)
    {
        if (provider is null || IsCurrent(provider, context)) return false;
        if (IsInvariant(provider, context)) return true;
        context.Report(call, ConversionSeverity.Error, "EQ2108",
            $"Only CultureInfo.InvariantCulture and CultureInfo.CurrentCulture cross for {doing}: the browser "
            + "knows the page's culture, which is the current one, and the invariant one. Use one of those.");
        return false;
    }

    /// <summary>Whether a culture argument is a null, by the model or, with none, by its spelling.</summary>
    public static bool IsNull(ExpressionSyntax provider, ConversionContext context) =>
        context.SemanticHelper.KnowsOrMapped(provider)
            ? context.SemanticHelper.IsNullConstant(provider)
            : Bare(provider).Kind() is Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression
                or Microsoft.CodeAnalysis.CSharp.SyntaxKind.DefaultLiteralExpression
                or Microsoft.CodeAnalysis.CSharp.SyntaxKind.DefaultExpression;

    /// <summary>The culture a cast or parentheses hand on: <c>(IFormatProvider)CultureInfo.InvariantCulture</c>
    /// is the invariant culture. With a model, only a cast that keeps the object does, an identity or
    /// a reference conversion; a user-defined one makes another value. With none, the spelling decides.</summary>
    private static ExpressionSyntax Named(ExpressionSyntax provider, ConversionContext context)
    {
        while (true)
        {
            switch (provider)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    provider = parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast when !context.SemanticHelper.KnowsOrMapped(cast)
                    || context.SemanticHelper.GetOperation(cast) is Microsoft.CodeAnalysis.Operations.IConversionOperation
                        { Conversion: { IsIdentity: true } or { IsReference: true } }:
                    provider = cast.Expression;
                    continue;
                default:
                    return provider;
            }
        }
    }

    /// <summary>The expression a cast and parentheses name: <c>(IFormatProvider?)null</c> is a null
    /// by its spelling too.</summary>
    private static ExpressionSyntax Bare(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => Bare(parenthesized.Expression),
        CastExpressionSyntax cast => Bare(cast.Expression),
        _ => expression,
    };

    private static bool Names(ExpressionSyntax provider, string property, ConversionContext context)
    {
        provider = Named(provider, context);
        if (context.SemanticHelper.KnowsOrMapped(provider))
        {
            return context.SemanticHelper.GetSymbol(provider) is IPropertySymbol
                {
                    IsStatic: true,
                    ContainingType: { Name: "CultureInfo", ContainingNamespace: var home },
                } culture
                && culture.Name == property
                && home.ToDisplayString() == "System.Globalization";
        }
        return provider is MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name, Expression: var owner }
            && name == property
            && owner.ToString() is "CultureInfo" or "System.Globalization.CultureInfo"
                or "global::System.Globalization.CultureInfo";
    }
}
