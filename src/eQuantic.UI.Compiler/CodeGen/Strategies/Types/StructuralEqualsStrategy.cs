using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
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
/// <para>
/// A tuple and an anonymous object are compared where the static types still say what the browser cannot:
/// the other value's type before C# boxed it into <c>Equals(object)</c>. One of another tuple or anonymous
/// type (other element or member types, other member names) is unequal before a member is read, as .NET's
/// <c>Equals</c> checks the type first, where the browser holds both as the same array or plain object. An
/// anonymous object's call is an instance call, which throws through null after its argument is evaluated
/// (<see cref="Eq.EqualsInstance"/>), where a tuple, a value, is never null and an empty <c>Nullable</c>
/// one equals only null.
/// </para>
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
        var type = context.SemanticHelper.GetType(ma.Expression);
        if (type.UnwrapNullable() is not INamedTypeSymbol named || !(named.IsTupleType || named.IsAnonymousType))
            return JsExpr.Call(JsExpr.Identifier(Eq.Equals), receiver, other);

        // A tuple's Equals compares each element with that element type's default comparer, an array
        // by reference, which only the static type says: a tuple is an array here (#554). An anonymous
        // object's compares its members so too, and takes only another of the same members in the same
        // order for one of its type.
        var another = OfAnotherType(inv, named, context);
        // An empty Nullable tuple equals only null, and one with a value nothing of another type: both
        // values are still evaluated, as the call's receiver and argument are.
        if (another && type.IsNullableValue())
            return JsExpr.Call(JsExpr.Identifier(Eq.LogicAnd), JsExpr.Binary(receiver, "==", JsExpr.Literal("null")),
                JsExpr.Binary(other, "==", JsExpr.Literal("null")));
        var comparison = ElementEquality.OwnEquals(named);
        if (named.IsAnonymousType || another)
            return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other,
                another ? JsExpr.Literal("null") : JsExpr.Opaque(comparison!));
        return JsExpr.Call(comparison is null ? JsExpr.Identifier(Eq.Equals) : JsExpr.Opaque(comparison), receiver, other);
    }

    /// <summary>
    /// Whether the other value of <c>Equals(object)</c> is, by its type before C# boxed it, of a type no
    /// value of <paramref name="receiver"/> can be: a tuple or an anonymous object of other element or
    /// member types, or any other concrete type, which .NET finds unequal before it reads a member. Its
    /// type is read through the conversions that keep the value what it is (a boxing, a reference
    /// conversion, an identity, a cast to <c>object</c>), and a <c>Nullable</c> of the receiver's type is
    /// the receiver's. False where the type could hold one: <c>object</c>, <c>ValueType</c>, an interface,
    /// a type parameter, <c>dynamic</c>, a value with no type of its own (a <c>null</c>); and for the
    /// strongly typed <c>Equals(T)</c> of a tuple, whose argument C# has already converted to the
    /// receiver's type. The element names of a tuple are no part of its type, as .NET's closed type erases
    /// them: an identity conversion is the same type.
    /// </summary>
    private static bool OfAnotherType(InvocationExpressionSyntax invocation, INamedTypeSymbol receiver, ConversionContext context)
    {
        if (context.SemanticModel is not { } model
            || context.SemanticHelper.GetOperation(invocation) is not IInvocationOperation
            {
                TargetMethod.Parameters: [{ Type.SpecialType: SpecialType.System_Object }],
                Arguments: [var argument],
            })
            return false;
        var value = argument.Value;
        while (value is IConversionOperation conversion
               && conversion.GetConversion() is { IsBoxing: true } or { IsReference: true } or { IsIdentity: true })
            value = conversion.Operand;
        var type = value.Type.UnwrapNullable() ?? value.Type;
        if (type is null
            || type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum
            || type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Dynamic or TypeKind.Error)
            return false;
        return !model.Compilation.ClassifyCommonConversion(type, receiver).IsIdentity;
    }

    public int Priority => 12;
}
