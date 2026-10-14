using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Routes <c>value.Equals(other)</c> on a record, a user struct, a value tuple or an anonymous object to
/// the structural equality its type compares by: <c>$eq.equals</c>, or, for a tuple and an anonymous
/// object, the comparison <see cref="ElementEquality"/> generates from their members' types. These are
/// plain objects and arrays at runtime, so the default <c>.equals(...)</c> emission would hit a missing
/// method: an anonymous object's threw <c>equals is not a function</c> (#743), as its type is a class and
/// not a structural value type. Gated strictly on the receiver's type (string keeps its own handling).
/// Priority 12, above the generic <see cref="Expressions.InvocationStrategy"/> fallback.
/// </summary>
public class StructuralEqualsStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv)
            return false;
        if (ma.Name.Identifier.Text != "Equals") return false;
        if (inv.ArgumentList.Arguments.Count != 1) return false;
        // A bare `object` may hold a boxed value, and .NET compares boxed values by VALUE —
        // `((object)5).Equals((object)5)` is true. The helper already answers that for primitives.
        // A Nullable tuple's Equals is its value's, or, empty, whether the other value is null too, which
        // both comparisons answer for a null; its call went to an `equals` no array has.
        var receiver = context.SemanticHelper.GetType(ma.Expression);
        return receiver.IsStructuralValueType() || receiver is { IsAnonymousType: true }
            || receiver.UnwrapNullable() is INamedTypeSymbol { IsTupleType: true }
            || receiver?.SpecialType == SpecialType.System_Object;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var inv = (InvocationExpressionSyntax)node;
        var ma = (MemberAccessExpressionSyntax)inv.Expression;
        var receiver = context.Converter.ConvertIr(ma.Expression);
        var other = context.Converter.ConvertIr(inv.ArgumentList.Arguments[0].Expression);
        context.UsedHelpers.Add(Eq.Import);
        // A tuple's Equals compares each element with that element type's default comparer, an array
        // by reference, which only the static type says: a tuple is an array here (#554). An anonymous
        // object's compares its members so too, and takes only another of the same members in the same
        // order for one of its type.
        var comparison = context.SemanticHelper.GetType(ma.Expression).UnwrapNullable() is INamedTypeSymbol named
            && (named.IsTupleType || named.IsAnonymousType)
            ? ElementEquality.OwnEquals(named)
            : null;
        return JsExpr.Call(comparison is null ? JsExpr.Identifier(Eq.Equals) : JsExpr.Opaque(comparison), receiver, other);
    }

    public int Priority => 12;
}
