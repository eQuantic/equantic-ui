using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// <c>a ?? b</c>. As a <see cref="JsBinary"/> the writer can enforce the rule that makes this
/// operator dangerous: JavaScript REFUSES <c>??</c> unparenthesized beside <c>&amp;&amp;</c> or
/// <c>||</c>, while C# needs no parentheses there at all.
/// </summary>
public class NullCoalescingStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is BinaryExpressionSyntax binary &&
               binary.IsKind(SyntaxKind.CoalesceExpression);
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var binary = (BinaryExpressionSyntax)node;
        return JsExpr.Binary(
            context.Converter.ConvertIr(binary.Left), "??",
            context.Converter.ConvertIr(binary.Right));
    }

    public int Priority => 10; // Higher than BinaryExpressionStrategy
}
