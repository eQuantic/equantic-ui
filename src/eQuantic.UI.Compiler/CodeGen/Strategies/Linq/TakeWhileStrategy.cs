using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .TakeWhile(predicate) to a custom JS reduction.
/// </summary>
public class TakeWhileStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "TakeWhile") return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType)) return true;

        // Fallback
        return symbol == null && context.CanGuess(node);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 1)
        {
            var predicate = context.Converter.ConvertExpression(args[0].Expression);
            // JS doesn't have native TakeWhile. We use a reduce trick or a helper.
            // Inline implementation:
            // source.reduce((acc, x) => { if (acc._taking && predicate(x)) acc.res.push(x); else acc._taking = false; return acc; }, { res: [], _taking: true }).res
            
            // However, that's verbose and iterates the whole array.
            // Better approach for small arrays in UI:
            // (function(arr) { const res = []; for(const x of arr) { if(predicate(x)) res.push(x); else break; } return res; })(source)
            
            return $"(function(arr) {{ const res = []; for(const x of arr) {{ if(({predicate})(x)) res.push(x); else break; }} return res; }})({caller})";
        }

        return caller;
    }

    public int Priority => 10;
}
