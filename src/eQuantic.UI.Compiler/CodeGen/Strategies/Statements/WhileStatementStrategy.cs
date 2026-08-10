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
        // A pattern variable bound in the CONDITION is assigned on every test, so its declaration
        // has to sit outside the loop — inside, it would be redeclared each pass, and the loop is
        // the one place the condition runs more than once.
        var hoisted = PatternVariableScanner.Declarations(whileStmt.Condition);
        var condition = context.Converter.ConvertExpression(whileStmt.Condition);
        var body = context.Converter.Convert(whileStmt.Statement);

        return $"{hoisted}while ({condition}) {body}";
    }

    public int Priority => 0;
}
