using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for do-while loop statements.
/// Handles:
/// - do { ... } while (condition);
/// </summary>
public class DoWhileStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is DoStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var doStmt = (DoStatementSyntax)node;
        var condition = context.Converter.ConvertExpression(doStmt.Condition);
        var body = context.Converter.Convert(doStmt.Statement);

        return $"do {body} while ({condition});";
    }

    public int Priority => 0;
}
