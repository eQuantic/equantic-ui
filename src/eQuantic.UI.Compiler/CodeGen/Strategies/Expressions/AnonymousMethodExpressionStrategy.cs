using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary><c>delegate (x) { … }</c> — an arrow with a block body.</summary>
public class AnonymousMethodExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AnonymousMethodExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var anon = (AnonymousMethodExpressionSyntax)node;
        var parameters = anon.ParameterList is null
            ? ""
            : string.Join(", ", anon.ParameterList.Parameters.Select(p => p.Identifier.Text));
        return JsExpr.ArrowBlock(parameters, context.Converter.ConvertBlock(anon.Block));
    }

    public int Priority => 10;
}
