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
        // `async delegate { … }` keeps its async, for the same reason the local function does:
        // an arrow that is not async makes `await` in its body a SyntaxError, and the module then
        // fails to parse rather than misbehaving somewhere visible.
        var isAsync = anon.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
        return JsExpr.ArrowBlock(parameters, context.Converter.ConvertBlock(anon.Block), isAsync);
    }

    public int Priority => 10;
}
