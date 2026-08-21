using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>while (cond) body</c>, pattern bindings of the condition hoisted in front.</summary>
public class WhileStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is WhileStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var whileStmt = (WhileStatementSyntax)node;
        var hoisted = PatternVariableScanner.Declarations(whileStmt.Condition, context.TypeAnnotations);
        var condition = context.Converter.ConvertIr(whileStmt.Condition);
        var body = context.Converter.ConvertStatementIr(whileStmt.Statement);
        return JsStatement.Hoisted(hoisted, JsStatement.While(condition, body));
    }

    public int Priority => 0;
}
