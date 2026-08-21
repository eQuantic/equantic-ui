using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>lock (o) body</c> — single-threaded on the other side, so the body alone.</summary>
public class LockStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LockStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context) =>
        context.Converter.ConvertStatementIr(((LockStatementSyntax)node).Statement);

    public int Priority => 10;
}
