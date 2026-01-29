using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for continue statements.
/// Handles:
/// - continue; (skip to next loop iteration)
/// </summary>
public class ContinueStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ContinueStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        return "continue;";
    }

    public int Priority => 0;
}
