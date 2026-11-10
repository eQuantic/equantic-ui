using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts <c>enumValue.HasFlag(flag)</c>: the bitwise test <c>(value &amp; flag) === flag</c> on the values,
/// at the underlying type's width (<see cref="EnumOperators.HasFlag"/>), which .NET answers for any enum: a
/// non-flags one degraded to equality, so a member with no bits was in no other value, and a uint flag's
/// high bit read negative on one side of the test (#555).
/// </summary>
public class EnumHasFlagStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.ValueText != "HasFlag" || invocation.ArgumentList.Arguments.Count != 1)
            return false;

        // HasFlag is declared on System.Enum. Confirm the receiver is an enum when we have semantics;
        // without a model, trust the (enum-specific) method name.
        var receiverType = context.SemanticHelper.GetType(memberAccess.Expression);
        return receiverType == null || receiverType.TypeKind == TypeKind.Enum;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var receiver = context.Converter.ConvertIr(memberAccess.Expression);
        var flagSyntax = invocation.ArgumentList.Arguments[0].Expression;
        var flag = context.Converter.ConvertIr(flagSyntax);
        var flagConstant = context.SemanticHelper.TryGetConstantValue(flagSyntax, out var constant) && constant is not null
            ? System.Convert.ToDecimal(constant, System.Globalization.CultureInfo.InvariantCulture)
            : (decimal?)null;
        return context.SemanticHelper.GetType(memberAccess.Expression) is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            ? JsExprWriter.Write(EnumOperators.HasFlag(enumType, receiver, flag, flagConstant, context))
            : $"(({JsExprWriter.Write(receiver)} & {JsExprWriter.Write(flag)}) === {JsExprWriter.Write(flag)})";
    }

    // Above the generic invocation handling.
    public int Priority => 20;
}
