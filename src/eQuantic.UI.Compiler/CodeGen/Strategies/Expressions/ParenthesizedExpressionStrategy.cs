using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// The author's parentheses become a <see cref="JsGroup"/>: the writer keeps them wherever it
/// cannot see the surroundings (text handed to an unmigrated consumer, or opaque text inside) and
/// re-derives them wherever it can — so <c>((x)) + 1</c> sheds its redundant pair while
/// <c>(a + b) * c</c> keeps its necessary one, by the same rule.
/// </summary>
public class ParenthesizedExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ParenthesizedExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var parens = (ParenthesizedExpressionSyntax)node;
        return JsExpr.Group(context.Converter.ConvertIr(parens.Expression));
    }

    public int Priority => 10;
}
