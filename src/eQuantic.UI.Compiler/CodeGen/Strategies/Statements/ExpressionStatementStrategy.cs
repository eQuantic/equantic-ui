using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class ExpressionStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ExpressionStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var exprStmt = (ExpressionStatementSyntax)node;
        // `is` pattern bindings assign inside the converted expression — their `let`s hoist here
        // (C# scopes them to the enclosing block; see PatternVariableScanner).
        var declarations = PatternVariableScanner.Declarations(exprStmt.Expression, context.TypeAnnotations);
        return declarations + context.Converter.ConvertExpression(exprStmt.Expression) + ";";
    }

    public int Priority => 0;
}
