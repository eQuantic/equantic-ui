using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>lock (o) body</c> — single-threaded on the other side, so the body alone.</summary>
public class LockStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LockStatementSyntax;
    }

    /// <summary>
    /// The body alone: a page has one thread, so there is nothing to hold. Unless the expression
    /// DECLARES something (<c>lock (Gate(out var held))</c>): Roslyn scopes that to the enclosing
    /// block and the body may read it, so the expression runs, in front, where it is declared.
    /// </summary>
    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var lockStmt = (LockStatementSyntax)node;
        var body = context.Converter.ConvertStatementIr(lockStmt.Statement);
        var declared = ExpressionVariableScanner.Declarations(lockStmt.Expression, context.TypeAnnotations);
        if (declared.Length == 0) return body;
        var expression = JsStatement.Expression(context.Converter.ConvertIr(lockStmt.Expression));
        return JsStatement.Sequence(JsStatement.Raw(declared.TrimEnd()), expression, body);
    }

    public int Priority => 10;
}
