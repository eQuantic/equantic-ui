using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .SkipWhile(predicate) to a custom JS IIFE.
/// </summary>
public class SkipWhileStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "SkipWhile") return false;

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
            var predicate = context.Converter.ConvertExpression(args[0].Expression);
            // Inline implementation:
            // (function(arr) { const res = []; let skipping = true; for(const x of arr) { if(skipping && predicate(x)) continue; skipping = false; res.push(x); } return res; })(source)
            
            return $"(function(arr) {{ const res = []; let skipping = true; for(const x of arr) {{ if(skipping && {predicate}(x)) continue; skipping = false; res.push(x); }} return res; }})({caller})";
        }

        return caller;
    }

    public int Priority => 10;
}
