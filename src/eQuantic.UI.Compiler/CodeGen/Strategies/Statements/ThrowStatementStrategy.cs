using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for throw statements.
/// Handles:
/// - throw new Exception("message");
/// - throw ex;
/// </summary>
public class ThrowStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ThrowStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var throwStmt = (ThrowStatementSyntax)node;

        if (throwStmt.Expression == null)
        {
            // Re-throw: throw;
            return "throw;";
        }

        var exception = context.Converter.ConvertExpression(throwStmt.Expression);
        return $"throw {exception};";
    }

    public int Priority => 0;
}
