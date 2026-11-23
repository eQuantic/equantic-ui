using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// A culture's <c>NumberFormatInfo</c>: <c>NumberFormatInfo.CurrentInfo</c> and
/// <c>CultureInfo.CurrentCulture.NumberFormat</c>, the page's culture's, and
/// <c>NumberFormatInfo.InvariantInfo</c> and <c>CultureInfo.InvariantCulture.NumberFormat</c>, the
/// invariant one's. The browser already holds that object: the page's culture travels as .NET's own
/// <c>NumberFormatInfo</c>, under .NET's names (<c>CultureFormatBridge</c>), so the read is the object
/// and its members are read by name (<see cref="PlatformMemberRead"/>). Each was EQ2004 (#847) where a
/// money field asks the culture which side its currency's symbol goes on (#1765).
/// <para>
/// Claimed whole, before its receiver is converted: <c>CultureInfo.CurrentCulture</c> read alone
/// carries nothing and stays refused. A culture held in a variable, or the UI culture's format, has no
/// counterpart in the browser and is not claimed.
/// </para>
/// </summary>
public class NumberFormatInfoStrategy : IExpressionIrStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context) => Culture(node, context) is not null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.NumberFormat), JsExpr.Literal(Culture(node, context) is true ? "true" : "false"));
    }

    /// <summary>True for a read of the invariant culture's number format, false for the page's, and null
    /// for anything else.</summary>
    private static bool? Culture(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax access
            || context.SemanticHelper.GetSymbol(node) is not IPropertySymbol property)
            return null;

        return (property.ContainingType.ToDisplayString(), property.Name) switch
        {
            ("System.Globalization.NumberFormatInfo", "CurrentInfo") => false,
            ("System.Globalization.NumberFormatInfo", "InvariantInfo") => true,
            ("System.Globalization.CultureInfo", "NumberFormat")
                when NamedCulture.IsInvariant(access.Expression, context) => true,
            ("System.Globalization.CultureInfo", "NumberFormat")
                when !NamedCulture.IsNull(access.Expression, context) && NamedCulture.IsCurrent(access.Expression, context) => false,
            _ => null,
        };
    }
}
