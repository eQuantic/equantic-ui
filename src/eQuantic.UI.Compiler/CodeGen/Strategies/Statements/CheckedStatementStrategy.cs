using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class CheckedStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is CheckedStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var checkedStmt = (CheckedStatementSyntax)node;
        // JS doesn't support checked/unchecked overflow contexts.
        // We just verify the block contents.
        return context.Converter.ConvertBlock(checkedStmt.Block);
    }

    public int Priority => 10;
}
