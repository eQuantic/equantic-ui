using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Routes <c>value.Equals(other)</c> on a record / user struct / value tuple to the structural
/// equality helper (<c>$eq.equals</c>). These are plain objects/arrays at runtime, so the default
/// <c>.equals(...)</c> emission would hit a missing method. Gated strictly on the receiver being a
/// structural value type (string keeps its own handling). Priority 12 — above the generic
/// <see cref="Expressions.InvocationStrategy"/> fallback.
/// </summary>
public class StructuralEqualsStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv)
            return false;
        if (ma.Name.Identifier.Text != "Equals") return false;
        if (inv.ArgumentList.Arguments.Count != 1) return false;
        // A bare `object` may hold a boxed value, and .NET compares boxed values by VALUE —
        // `((object)5).Equals((object)5)` is true. The helper already answers that for primitives.
        var receiver = context.SemanticHelper.GetType(ma.Expression);
        return receiver.IsStructuralValueType() || receiver?.SpecialType == SpecialType.System_Object;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var inv = (InvocationExpressionSyntax)node;
        var ma = (MemberAccessExpressionSyntax)inv.Expression;
        var receiver = context.Converter.ConvertExpression(ma.Expression);
        var other = context.Converter.ConvertExpression(inv.ArgumentList.Arguments[0].Expression);
        context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.Equals}({receiver}, {other})";
    }

    public int Priority => 12;
}
