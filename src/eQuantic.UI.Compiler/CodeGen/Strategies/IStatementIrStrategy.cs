using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A statement strategy that has crossed over to the statement IR: it builds a
/// <see cref="JsStatement"/> and lets <see cref="JsStatementWriter"/> lay it out. Implementing this
/// is the whole migration — <see cref="IStatementStrategy.Convert"/> comes for free, rendered in the
/// context's layout at the context's depth.
/// </summary>
public interface IStatementIrStrategy : IStatementStrategy
{
    JsStatement ConvertIr(StatementSyntax node, ConversionContext context);

    string IStatementStrategy.Convert(StatementSyntax node, ConversionContext context) =>
        JsStatementWriter.Write(ConvertIr(node, context), context.Layout, context.Depth);
}
