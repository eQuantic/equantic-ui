using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class ReturnStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ReturnStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var retStmt = (ReturnStatementSyntax)node;
        if (retStmt.Expression != null)
        {
            var expr = context.Converter.ConvertExpression(retStmt.Expression);
            return $"return {expr};";
        }
        return "return;";
    }

    public int Priority => 0;
}
