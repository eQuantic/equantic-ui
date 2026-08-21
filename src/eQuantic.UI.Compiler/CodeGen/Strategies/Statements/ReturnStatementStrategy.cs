using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>return;</c> and <c>return expr;</c>, with any pattern bindings the expression
/// introduces hoisted in front (C# scopes them to the enclosing block).</summary>
public class ReturnStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ReturnStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var retStmt = (ReturnStatementSyntax)node;
        if (retStmt.Expression == null) return JsStatement.Return(null);

        var declarations = PatternVariableScanner.Declarations(retStmt.Expression, context.TypeAnnotations);
        var value = context.Converter.ConvertIr(retStmt.Expression);
        return JsStatement.Hoisted(declarations, JsStatement.Return(value));
    }

    public int Priority => 0;
}
