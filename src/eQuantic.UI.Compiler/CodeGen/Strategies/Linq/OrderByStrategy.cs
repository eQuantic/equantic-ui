using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

public class OrderByStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax access)
        {
            return access.Name.Identifier.Text == "OrderBy" || access.Name.Identifier.Text == "OrderByDescending";
        }
        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(access.Expression);
        var methodName = access.Name.Identifier.Text;
        
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return $"{caller}.sort()";

        var keySelector = context.Converter.ConvertExpression(args[0].Expression);
        
        // Use a generic sort comparator in JS that utilizes the key selector
        // We wrap the selector to avoid scope issues
        // (a, b) => key(a) > key(b) ? 1 : -1
        
        var comparison = methodName == "OrderBy" 
            ? "ka > kb ? 1 : -1" 
            : "ka < kb ? 1 : -1";

        return $"{caller}.sort((a, b) => {{ const key = {keySelector}; const ka = key(a); const kb = key(b); return {comparison}; }})";
    }

    public int Priority => 10;
}
