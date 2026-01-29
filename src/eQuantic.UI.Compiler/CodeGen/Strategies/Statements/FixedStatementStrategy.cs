using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for Fixed statements.
/// Handles: fixed(int* p = &x) { ... }
/// Since JS doesn't support pointers/pinning, this treats it as a standard block 
/// but warns about potential unsafe behavior mismatch.
/// </summary>
public class FixedStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is FixedStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var fixedStmt = (FixedStatementSyntax)node;
        
        // Convert the body
        var body = context.Converter.ConvertStatement(fixedStmt.Statement);
        
        // Optional: Emit a comment warning
        return $"/* fixed statement unwrapped */ {body}";
    }

    public int Priority => 10;
}
