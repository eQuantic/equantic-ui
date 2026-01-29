using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for Lock statements.
/// Handles: lock(obj) { ... } -> { ... } (No-op wrapper as JS is single threaded)
/// </summary>
public class LockStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LockStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var lockStmt = (LockStatementSyntax)node;
        // Ignore the lock object, just convert the body
        // We wrap in a block to maintain scope if needed, or just emit the block
        // Lock body is usually a Statement (Block or single statement)
        
        return context.Converter.ConvertStatement(lockStmt.Statement);
    }

    public int Priority => 10;
}
