using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// A primitive's own <c>Equals</c>: its typed overload and <c>Equals(object)</c>, on every primitive
/// (#956, #957). .NET's <c>Equals(object)</c> is true only for a value of the receiver's own type, and the
/// browser holds an int and a double as one kind of number and a char and a text of one as one kind of
/// string, so the other value's type is read where the bound tree still shows it, before C# boxed it. One
/// of another type answers false with both values evaluated. A value already held as <c>object</c> has lost
/// its type and compares by what it holds: an int and a double of one value, or a char and a text of one,
/// are found equal there, which is the limit of a value held as <c>object</c> (#552).
/// A double and a float go through <c>$eq.equals</c>, where NaN equals NaN as their <c>Equals</c> holds it,
/// and a decimal, which is an object, through its own <c>equals</c>.
/// </summary>
public class PrimitiveEqualsStrategy : IExpressionIrStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax invocation
        && invocation.TryGetInstanceCall(out _, out _)
        && context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { Name: "Equals", IsStatic: false, Parameters.Length: 1 } method
        && method.ContainingType.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char
            or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_IntPtr or SpecialType.System_UIntPtr
            or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        invocation.TryGetInstanceCall(out var receiverSyntax, out _);

        var home = ((IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!).ContainingType;
        var receiver = context.Converter.ConvertIr(receiverSyntax);
        var other = context.Converter.ConvertIr(invocation.ArgumentList.Arguments[0].Expression);

        if (StructuralEqualsStrategy.OfAnotherType(invocation, home, context))
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.EqualsInstance), receiver, other, JsExpr.Literal("null"));
        }

        if (home.SpecialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal)
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.Equals), receiver, other);
        }

        return JsExpr.Template("({0} === {1})", receiver, other);
    }
}
