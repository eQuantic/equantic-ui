using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts <c>enumValue.HasFlag(flag)</c>: whether every bit of the flag's value is set in the
/// receiver's (<see cref="EnumOperators.HasFlag"/>), at the underlying type's width, for any enum. A
/// non-flags enum answered equality, which .NET does not (a member with no bits is in every value),
/// and a uint's high bit compared negative with positive.
/// </summary>
public class EnumHasFlagStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "HasFlag" || invocation.ArgumentList.Arguments.Count != 1)
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
        var flag = context.Converter.ConvertIr(invocation.ArgumentList.Arguments[0].Expression);

        // Without a model the method name is all there is, and the bitwise test is what it names.
        return JsExprWriter.Write(EnumOperators.EnumOf(context.SemanticHelper.GetType(memberAccess.Expression)) is { } enumType
            ? EnumOperators.HasFlag(enumType, receiver, flag, context)
            : JsExpr.Template("(({0} & {1}) === {1})", [receiver, flag], context.TypeAnnotations));
    }

    // Above the generic invocation handling.
    public int Priority => 20;
}
