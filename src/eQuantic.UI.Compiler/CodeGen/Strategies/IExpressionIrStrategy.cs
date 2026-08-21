using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A strategy that has crossed over to the expression IR: it builds a <see cref="JsExpr"/> and
/// lets <see cref="JsExprWriter"/> decide the punctuation. Implementing this is the whole
/// migration — <see cref="IConversionStrategy.Convert"/> comes for free, so both callers (the
/// string world and the IR world) keep working while the move happens one strategy at a time.
/// </summary>
public interface IExpressionIrStrategy : IConversionStrategy
{
    JsExpr ConvertIr(SyntaxNode node, ConversionContext context);

    string IConversionStrategy.Convert(SyntaxNode node, ConversionContext context) =>
        JsExprWriter.Write(ConvertIr(node, context));
}
