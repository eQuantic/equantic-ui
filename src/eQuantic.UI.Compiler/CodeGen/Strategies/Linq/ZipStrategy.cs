using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Zip.
/// Handles: source.Zip(second, (a, b) => ...), and the overloads without a result selector,
/// source.Zip(second) and source.Zip(second, third), which answer value tuples (arrays here) through the
/// runtime's <c>$eq.linq.zipTuples</c> (#722): the three sequences were taken for the overload with a
/// selector, which called the third as a function, and the pair form answered its receiver.
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
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method
            && !method.Parameters.Any(parameter => parameter.Name == "resultSelector"))
        {
            var tuples = LinqArguments.Call(Eq.LinqZipTuples, LinqSource.Ir(memberAccess.Expression, context), ["second", "third"],
                invocation, argument => LinqSource.Argument(argument, invocation, context), context);
            return tuples is null ? context.Unhandled(invocation, "Zip without a result selector") : Ir.JsExprWriter.Write(tuples);
        }

        var source = LinqSource.Text(memberAccess.Expression, context);
        var args = invocation.ArgumentList.Arguments;
        
        if (args.Count >= 2)
        {
            var second = Ir.JsExprWriter.Write(LinqSource.Argument(args[0], invocation, context));
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
