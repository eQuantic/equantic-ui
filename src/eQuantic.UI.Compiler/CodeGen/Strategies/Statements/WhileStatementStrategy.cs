using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class WhileStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is WhileStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var whileStmt = (WhileStatementSyntax)node;
        var condition = context.Converter.ConvertExpression(whileStmt.Condition);
        var body = context.Converter.Convert(whileStmt.Statement);
        
        return $"while ({condition}) {body}";
    }

    public int Priority => 0;
}
