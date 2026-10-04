using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A <c>throw</c> expression (<c>x ?? throw new X(…)</c>, <c>c ? a : throw …</c>, <c>=&gt; throw …</c>):
/// JavaScript's <c>throw</c> is a statement, so the exception is handed to the runtime's
/// <c>$eq.exceptions.raise(…)</c> as its argument, evaluated where C# evaluates it, in the caller's own
/// function. It was an arrow invoked in place, <c>(() =&gt; { throw … })()</c>, and an <c>await</c> in
/// the exception (<c>s ?? throw new X(await M())</c>) landed in that arrow, which is not async: the
/// module did not parse (#539).
/// </summary>
public class ThrowExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ThrowExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var throwExpr = (ThrowExpressionSyntax)node;
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.Raise), context.Converter.ConvertIr(throwExpr.Expression));
    }

    public int Priority => 10;
}
