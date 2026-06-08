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
        if (args.Count == 0) return $"[...{caller}].sort()";

        var keySelector = context.Converter.ConvertExpression(args[0].Expression);

        // C# OrderBy returns a new, stably-sorted sequence and never mutates the source.
        // Copy first ([...caller]); return 0 for equal keys so JS's stable sort keeps order.
        var comparison = methodName == "OrderBy"
            ? "ka < kb ? -1 : ka > kb ? 1 : 0"
            : "ka > kb ? -1 : ka < kb ? 1 : 0";

        return $"[...{caller}].sort((a, b) => {{ const key = {keySelector}; const ka = key(a); const kb = key(b); return {comparison}; }})";
    }

    public int Priority => 10;
}
