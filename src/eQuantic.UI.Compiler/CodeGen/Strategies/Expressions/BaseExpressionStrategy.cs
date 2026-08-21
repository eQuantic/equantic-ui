using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class BaseExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is BaseExpressionSyntax;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context) => JsExpr.Identifier("super");

    public int Priority => 10;
}
