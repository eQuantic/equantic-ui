using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for break statements.
/// Handles:
/// - break; (exit loop or switch)
/// </summary>
public class BreakStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is BreakStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        return "break;";
    }

    public int Priority => 0;
}
