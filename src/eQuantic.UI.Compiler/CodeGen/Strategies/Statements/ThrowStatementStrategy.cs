using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>throw expr;</c>, and the bare rethrow <c>throw;</c>, which rethrows the exception its clause
/// caught: the catch binds it as <see cref="TryStatementStrategy.Caught"/> whenever a clause rethrows.
/// It was written as JavaScript's <c>throw;</c>, which is a SyntaxError.
/// </summary>
public class ThrowStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is ThrowStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var throwStmt = (ThrowStatementSyntax)node;
        if (throwStmt.Expression == null) return JsStatement.Throw(JsExpr.Identifier(TryStatementStrategy.Caught));
        // What the expression declares is hoisted in front, as a return's is (enclosing block).
        var declared = ExpressionVariableScanner.InFrontOf(throwStmt, throwStmt.Expression, context.TypeAnnotations);
        var exception = context.Converter.ConvertIr(throwStmt.Expression);
        // An exception that may be null is thrown as the NullReferenceException the CLR throws in its
        // place, so a typed catch sees one: a creation never is null, and neither is what the
        // nullable flow analysis proved not null.
        if (throwStmt.Expression is not BaseObjectCreationExpressionSyntax
            && !context.SemanticHelper.ProvedNotNull(throwStmt.Expression))
        {
            context.UsedHelpers.Add(Eq.Import);
            exception = JsExpr.Call(JsExpr.Identifier(Eq.Thrown), exception);
        }
        return JsStatement.Hoisted(declared, JsStatement.Throw(exception));
    }

    public int Priority => 0;
}
