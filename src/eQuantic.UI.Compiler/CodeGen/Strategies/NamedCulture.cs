using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Which culture a format provider NAMES, for the two policies that read one: formatting
/// (<c>ToString</c>) and reading a number from text (<see cref="ParseCulture"/>). Asked of the bound
/// symbol, so only <c>System.Globalization.CultureInfo</c>'s own properties count: a member of
/// another type that happens to be called <c>InvariantCulture</c> may return any culture, and its
/// getter may have an effect that leaving the argument out would lose. Where the model cannot be
/// asked (no model, or a node a strategy rewrote), the author's spelling decides, and only the
/// exact one: <c>CultureInfo.InvariantCulture</c>, with or without its namespace.
/// </summary>
internal static class NamedCulture
{
    public static bool IsInvariant(ExpressionSyntax provider, ConversionContext context) =>
        Names(provider, "InvariantCulture", context);

    public static bool IsCurrent(ExpressionSyntax provider, ConversionContext context) =>
        Names(provider, "CurrentCulture", context);

    private static bool Names(ExpressionSyntax provider, string property, ConversionContext context)
    {
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
