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
                $"?.{ToCamelCase(memberBinding.Name.Identifier.Text)}",

            // ?[index] -> ?.[index] (JavaScript requires the dot)
            ElementBindingExpressionSyntax elementBinding =>
                $"?.[{ConvertArguments(elementBinding.ArgumentList, context)}]",

            // ?.Method() -> ?.method()
            InvocationExpressionSyntax invocation when invocation.Expression is MemberBindingExpressionSyntax mb =>
                $"?.{ToCamelCase(mb.Name.Identifier.Text)}({ConvertArguments(invocation.ArgumentList, context)})",

            // Nested conditional access: a?.b?.c - The nested expression (b) is a MemberBindingExpression
            ConditionalAccessExpressionSyntax nested when nested.Expression is MemberBindingExpressionSyntax nestedMember =>
                $"?.{ToCamelCase(nestedMember.Name.Identifier.Text)}{ConvertWhenNotNull(nested.WhenNotNull, context)}",

            // Nested conditional access with identifier: for cases like user?.Address?.City
            ConditionalAccessExpressionSyntax nested =>
                $"?.{ToCamelCase(nested.Expression.ToString())}{ConvertWhenNotNull(nested.WhenNotNull, context)}",

            // Fallback
            _ => $"?.{context.Converter.ConvertExpression(whenNotNull)}"
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

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 15; // Higher priority to intercept before MemberAccessStrategy
}
