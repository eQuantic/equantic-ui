using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for GroupBy.
/// Handles: source.GroupBy(keySelector)
/// Maps to helper or Map.groupBy if supported, otherwise simple reduction.
/// </summary>
public class GroupByStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "GroupBy");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        
        if (args.Count == 0) return source;
        
        var keySelector = context.Converter.ConvertExpression(args[0].Expression);
        
        // Simple client-sidegroupBy implementation
        // Returns an array of { key, items } or Map
        return $"{source}.reduce((map, item) => {{ " +
               $"var key = ({keySelector})(item); " +
               $"var entry = map.find(e => e.key === key); " +
               $"if (!entry) {{ entry = {{ key, items: [] }}; map.push(entry); }} " +
               $"entry.items.push(item); return map; }}, [])";
    }

    public int Priority => 10;
}
