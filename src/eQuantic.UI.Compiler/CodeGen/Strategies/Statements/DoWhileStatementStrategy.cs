using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>do body while (cond);</c></summary>
public class DoWhileStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is DoStatementSyntax;
    }

    /// <summary>The variables the condition declares are the loop's own — Roslyn scopes them to the
    /// statement, so two sibling loops may repeat a name — and a block around the loop is where
    /// they are declared. Nothing but the condition can see them, so one per loop is .NET's
    /// answer too.</summary>
    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var doStmt = (DoStatementSyntax)node;
        var declared = ExpressionVariableScanner.Declarations(doStmt.Condition, context.TypeAnnotations);
        var condition = context.Converter.ConvertIr(doStmt.Condition);
        var body = context.Converter.ConvertStatementIr(doStmt.Statement);
        var loop = JsStatement.DoWhile(body, condition);
        return declared.Length == 0 ? loop : JsStatement.Block([JsStatement.Raw(declared.TrimEnd()), loop]);
    }

    public int Priority => 0;
}
