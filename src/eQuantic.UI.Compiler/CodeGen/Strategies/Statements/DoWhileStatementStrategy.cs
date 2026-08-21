using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>do body while (cond);</c></summary>
public class DoWhileStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is DoStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var doStmt = (DoStatementSyntax)node;
        var condition = context.Converter.ConvertIr(doStmt.Condition);
        var body = context.Converter.ConvertStatementIr(doStmt.Statement);
        return JsStatement.DoWhile(body, condition);
    }

    public int Priority => 0;
}
