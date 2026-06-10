using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts <c>enumValue.HasFlag(flag)</c>. A <c>[Flags]</c> enum is represented numerically (see
/// <see cref="EnumStrategy"/>), so <c>HasFlag</c> becomes the bitwise test <c>(value &amp; flag) === flag</c>
/// (true when every bit of <paramref name="flag"/> is set). For a non-flags enum (string repr) a single
/// value "has" only itself, so it degrades to equality.
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

        var receiver = context.Converter.ConvertExpression(memberAccess.Expression);
        var flag = context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[0].Expression);

        // Bitwise test for [Flags] (numeric) enums; equality for a known non-flags (string) enum.
        var receiverType = context.SemanticHelper.GetType(memberAccess.Expression);
        var knownNonFlags = receiverType is { TypeKind: TypeKind.Enum } && !receiverType.IsFlagsEnum();

        return knownNonFlags
            ? $"({receiver} === {flag})"
            : $"(({receiver} & {flag}) === {flag})";
    }

    // Above the generic invocation handling.
    public int Priority => 20;
}
