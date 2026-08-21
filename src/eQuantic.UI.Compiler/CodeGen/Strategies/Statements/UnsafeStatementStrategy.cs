using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>unsafe</c> is a context marker; the block inside transpiles as itself.</summary>
public class UnsafeStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context) => node is UnsafeStatementSyntax;

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context) =>
        context.Converter.ConvertBlockIr(((UnsafeStatementSyntax)node).Block);

    public int Priority => 10;
}
