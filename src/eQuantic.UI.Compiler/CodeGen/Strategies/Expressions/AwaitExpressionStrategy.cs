using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary><c>await x</c> — a prefix operator at unary level, so <c>(await x).y</c> keeps its
/// parentheses and <c>await x + 1</c> needs none, exactly as in C#.</summary>
public class AwaitExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AwaitExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var awaitExpr = (AwaitExpressionSyntax)node;
        return JsExpr.Prefix("await", context.Converter.ConvertIr(awaitExpr.Expression));
    }

    public int Priority => 10;
}
