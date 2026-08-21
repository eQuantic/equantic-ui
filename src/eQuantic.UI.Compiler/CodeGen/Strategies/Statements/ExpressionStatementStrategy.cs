using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>An expression used as a statement. `is` pattern bindings assign inside the converted
/// expression — their `let`s hoist in front (C# scopes them to the enclosing block; see
/// PatternVariableScanner).</summary>
public class ExpressionStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ExpressionStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var exprStmt = (ExpressionStatementSyntax)node;
        var declarations = PatternVariableScanner.Declarations(exprStmt.Expression, context.TypeAnnotations);
        var expression = context.Converter.ConvertIr(exprStmt.Expression);
        return JsStatement.Hoisted(declarations, JsStatement.Expression(expression));
    }

    public int Priority => 0;
}
