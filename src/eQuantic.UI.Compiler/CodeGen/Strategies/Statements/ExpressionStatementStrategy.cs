using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>An expression used as a statement. The variables it declares (a pattern's bindings, an
/// <c>out var</c>, a deconstruction's elements) are assigned inside the converted expression, so
/// their <c>let</c>s go in front: C# scopes them to the enclosing block (see
/// ExpressionVariableScanner).</summary>
public class ExpressionStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ExpressionStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var exprStmt = (ExpressionStatementSyntax)node;
        var declarations = ExpressionVariableScanner.Declarations(exprStmt.Expression, context.TypeAnnotations);
        var expression = context.Converter.ConvertIr(exprStmt.Expression);
        return JsStatement.Hoisted(declarations, JsStatement.Expression(expression));
    }

    public int Priority => 0;
}
