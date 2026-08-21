using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class ThisExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is ThisExpressionSyntax;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context) => JsExpr.This;

    public int Priority => 10;
}
