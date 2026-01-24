using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class IfStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is IfStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var ifStmt = (IfStatementSyntax)node;
        var condition = context.Converter.ConvertExpression(ifStmt.Condition);
        var ifTrue = context.Converter.Convert(ifStmt.Statement);
        
        var ifFalse = "";
        if (ifStmt.Else != null)
        {
            ifFalse = " else " + context.Converter.Convert(ifStmt.Else.Statement);
        }

        return $"if ({condition}) {ifTrue}{ifFalse}";
    }

    public int Priority => 0;
}
