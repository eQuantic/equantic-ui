using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for ToString method conversion.
/// Handles: x.ToString() → String(x)
/// This is safer than x.toString() in JS because String(x) handles null/undefined gracefully.
/// </summary>
public class ToStringStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        return memberAccess.Name.Identifier.Text == "ToString";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);

        // x.ToString("F2") → $eq.text.format(x, 'F2') via the runtime helper, mirroring the
        // format-specifier handling in interpolated strings. Without this the format
        // argument was silently dropped.
        var args = invocation.ArgumentList.Arguments;
        if (args.Count >= 1)
        {
            var fmt = context.Converter.ConvertExpression(args[0].Expression);
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.Format}({caller}, {fmt})";
        }

        return $"String({caller})";
    }

    public int Priority => 10;
}
