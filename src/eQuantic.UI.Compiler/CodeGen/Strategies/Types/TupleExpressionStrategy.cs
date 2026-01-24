using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using System.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts C# tuples (a, b) to JavaScript arrays [a, b].
/// </summary>
public class TupleExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is TupleExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var tuple = (TupleExpressionSyntax)node;
        var elements = tuple.Arguments.Select(arg => context.Converter.ConvertExpression(arg.Expression));
        return $"[{string.Join(", ", elements)}]";
    }

    public int Priority => 10;
}
