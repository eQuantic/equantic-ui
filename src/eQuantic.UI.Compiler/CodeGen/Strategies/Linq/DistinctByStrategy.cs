using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .DistinctBy(selector) to JS using a Map.
/// </summary>
public class DistinctByStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "DistinctBy") return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType)) return true;

        return context.SemanticModel == null;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 1)
        {
            var selector = context.Converter.ConvertExpression(args[0].Expression);
            // new Map(arr.map(x => [selector(x), x])).values() yields distinct items by key (takes last one)
            // C# DistinctBy takes the first one.
            // Correct approach: 
            // [...new Map(arr.map(x => [selector(x), x]).reverse()).values()].reverse() <-- inefficient
            // Better:
            // (arr => { const seen = new Set(); return arr.filter(x => { const k = selector(x); if(seen.has(k)) return false; seen.add(k); return true; }); })(source)
            
            return $"(arr => {{ const seen = new Set(); return arr.filter(x => {{ const k = ({selector})(x); if(seen.has(k)) return false; seen.add(k); return true; }}); }})({caller})";
        }

        return caller;
    }

    public int Priority => 10;
}
