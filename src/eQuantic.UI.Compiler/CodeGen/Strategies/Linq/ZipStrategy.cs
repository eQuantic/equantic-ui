using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Zip.
/// Handles: source.Zip(second, (a, b) => ...)
/// </summary>
public class ZipStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Zip");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        
        if (args.Count >= 2)
        {
            var second = context.Converter.ConvertExpression(args[0].Expression);
            var resultSelector = context.Converter.ConvertExpression(args[1].Expression);
            
            // source.map((e, i) => resultSelector(e, second[i]))
            // Only if second is array accessable. If iterable, harder.
            // Assuming array for UI simplified compilation.
            return $"{source}.map((e, i) => ({resultSelector})(e, {second}[i]))";
        }
        
        return source;
    }

    public int Priority => 10;
}
