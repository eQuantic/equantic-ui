using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
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
    /// The body, with nothing to hold: a page has one thread. The gate is still evaluated, once, in
    /// front, and refused when it is null, as C# evaluates it and <c>Monitor.Enter</c> refuses it before
    /// the body runs: a call in it ran nowhere (#475), a null gate let the body run, and one that
    /// DECLARES something (<c>lock (Gate(out var held))</c>) declares it where Roslyn scopes it, the
    /// enclosing block, for the body to read. Only <c>this</c>, which is never null, is left out.
    /// </summary>
    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var lockStmt = (LockStatementSyntax)node;
        var body = context.Converter.ConvertStatementIr(lockStmt.Statement);
        if (context.SemanticHelper.GetOperation(lockStmt.Expression) is IInstanceReferenceOperation)
            return body;
        context.UsedHelpers.Add(Eq.Import);
        var expression = JsStatement.Expression(
            JsExpr.Call(JsExpr.Identifier(Eq.LockGate), context.Converter.ConvertIr(lockStmt.Expression)));
        // Declared in front, unless a switch declares them for the section this lock stands in.
        var declared = ExpressionVariableScanner.InFrontOf(lockStmt, lockStmt.Expression, context.TypeAnnotations);
        return declared.Length == 0
            ? JsStatement.Sequence(expression, body)
            : JsStatement.Sequence(JsStatement.Raw(declared.TrimEnd()), expression, body);
    }

    public int Priority => 10;
}
