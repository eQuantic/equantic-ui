using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Except LINQ method.
/// Handles: source.Except(other) -> source.filter(x => !other.includes(x))
/// Returns elements from source that don't exist in other.
/// </summary>
public class ExceptStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Except");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return source;

        var other = context.Converter.ConvertExpression(args[0].Expression);

        // source.Except(other) -> [...new Set(source)].filter(x => !other.includes(x))
        // Use Set to remove duplicates from source, then filter out items in other
        return $"[...new Set({source})].filter(x => !{other}.includes(x))";
    }

    public int Priority => 10;
}
