using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// Strategy for Yield statements.
/// Handles: 
/// - yield return x -> yield x
/// - yield break -> return
/// </summary>
public class YieldStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is YieldStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var yieldStmt = (YieldStatementSyntax)node;
        
        if (yieldStmt.Kind() == SyntaxKind.YieldBreakStatement)
        {
            return "return;";
        }
        else
        {
            var expr = context.Converter.ConvertExpression(yieldStmt.Expression);
            return $"yield {expr};";
        }
    }

    public int Priority => 10;
}
