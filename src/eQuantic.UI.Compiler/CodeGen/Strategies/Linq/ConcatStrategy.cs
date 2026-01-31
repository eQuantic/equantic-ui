using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Concat LINQ method.
/// Handles: source.Concat(other) -> [...source, ...other]
/// </summary>
public class ConcatStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Concat");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return source;

        var other = context.Converter.ConvertExpression(args[0].Expression);

        // source.Concat(other) -> [...source, ...other]
        return $"[...{source}, ...{other}]";
    }

    public int Priority => 10;
}
