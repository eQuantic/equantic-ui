using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for bool static methods.
/// Handles: bool.Parse(s) -> (String(s).trim().toLowerCase() === 'true')
/// (.NET bool.Parse is case-insensitive and trims; it throws on invalid input, whereas this returns
/// false — a documented divergence. Strict/throwing parse will move to the eq compat helper.)
/// </summary>
public class BooleanMethodStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var expr = memberAccess.Expression.ToString();
        return (expr is "bool" or "Boolean" or "System.Boolean")
            && memberAccess.Name.Identifier.Text == "Parse";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return "false";

        var value = context.Converter.ConvertExpression(args[0].Expression);
        return $"(String({value}).trim().toLowerCase() === 'true')";
    }

    public int Priority => 10;
}
