using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for conditional access expressions (null-conditional operators).
/// Handles:
/// - ?. (conditional member access): a?.b -> a?.b
/// - ?[] (conditional element access): a?[0] -> a?.[0]
/// </summary>
public class ConditionalAccessStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ConditionalAccessExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var conditionalAccess = (ConditionalAccessExpressionSyntax)node;
        var expression = context.Converter.ConvertExpression(conditionalAccess.Expression);
        var whenNotNull = ConvertWhenNotNull(conditionalAccess.WhenNotNull, context);

        return $"{expression}{whenNotNull}";
    }

    private string ConvertWhenNotNull(ExpressionSyntax whenNotNull, ConversionContext context)
    {
        return whenNotNull switch
        {
            // ?.member -> ?.member
            MemberBindingExpressionSyntax memberBinding =>
                $"?.{memberBinding.Name.Identifier.Text.ToCamelCase()}",

            // ?[index] -> ?.[index] (JavaScript requires the dot)
            ElementBindingExpressionSyntax elementBinding =>
                $"?.[{ConvertArguments(elementBinding.ArgumentList, context)}]",

            // ?.Method() -> ?.method()
            InvocationExpressionSyntax invocation when invocation.Expression is MemberBindingExpressionSyntax mb =>
                mb.Name.Identifier.Text == "Invoke"
                    ? $"?.({ConvertArguments(invocation.ArgumentList, context)})"
                    : $"?.{mb.Name.Identifier.Text.ToCamelCase()}({ConvertArguments(invocation.ArgumentList, context)})",

            // ?.member.property -> ?.member.property (e.g., theme?.Alert.Title)
            MemberAccessExpressionSyntax memberAccess when memberAccess.Expression is MemberBindingExpressionSyntax binding =>
                $"?.{binding.Name.Identifier.Text.ToCamelCase()}.{memberAccess.Name.Identifier.Text.ToCamelCase()}",

            // ?.member.property.deeper -> chain after conditional (recursive)
            MemberAccessExpressionSyntax memberAccess =>
                $"?.{ConvertMemberChain(memberAccess, context)}",

            // Nested conditional access: a?.b?.c - The nested expression (b) is a MemberBindingExpression
            ConditionalAccessExpressionSyntax nested when nested.Expression is MemberBindingExpressionSyntax nestedMember =>
                $"?.{nestedMember.Name.Identifier.Text.ToCamelCase()}{ConvertWhenNotNull(nested.WhenNotNull, context)}",

            // Nested conditional access with identifier: for cases like user?.Address?.City
            ConditionalAccessExpressionSyntax nested =>
                $"?.{(nested.Expression.ToString()).ToCamelCase()}{ConvertWhenNotNull(nested.WhenNotNull, context)}",

            // Fallback
            _ => $"?.{context.Converter.ConvertExpression(whenNotNull)}"
        };
    }

    private string ConvertMemberChain(MemberAccessExpressionSyntax memberAccess, ConversionContext context)
    {
        var name = memberAccess.Name.Identifier.Text.ToCamelCase();
        return memberAccess.Expression switch
        {
            MemberBindingExpressionSyntax binding =>
                $"{binding.Name.Identifier.Text.ToCamelCase()}.{name}",
            MemberAccessExpressionSyntax nested =>
                $"{ConvertMemberChain(nested, context)}.{name}",
            _ => $"{context.Converter.ConvertExpression(memberAccess.Expression)}.{name}"
        };
    }

    private string ConvertArguments(BracketedArgumentListSyntax argumentList, ConversionContext context)
    {
        var args = argumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression));
        return string.Join(", ", args);
    }

    private string ConvertArguments(ArgumentListSyntax argumentList, ConversionContext context)
    {
        var args = argumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression));
        return string.Join(", ", args);
    }

    public int Priority => 15; // Higher priority to intercept before MemberAccessStrategy
}
