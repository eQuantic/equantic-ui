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
        return context.IsLinqMethod(node, "Zip");
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
            
            // A map over the receiver walks the LONGER sequence and hands the selector undefined
            // for the missing partner — a silent NaN for numbers. LINQ stops with the shorter.
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.Zip}({source}, {second}, {resultSelector})";
        }
        
        return source;
    }

    public int Priority => 10;
}
