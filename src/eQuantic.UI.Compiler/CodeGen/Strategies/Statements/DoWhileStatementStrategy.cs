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

    /// <summary>
    /// A condition that declares variables (<c>while (Next(out var n))</c> after the body) becomes
    /// <c>for (let n, $again = true; $again; $again = cond) body</c>. Roslyn keeps them inside the
    /// loop, so two sibling loops may repeat a name, and .NET gives every iteration its own: a closure
    /// the condition makes keeps its iteration's value (.NET 12, where one variable per loop answered
    /// 0). A <c>for</c> head's <c>let</c> is the binding JavaScript copies for each iteration, and the
    /// flag runs the body first and the condition after it, a <c>continue</c> included, as a
    /// <c>do</c> does. <c>$</c> cannot begin a C# identifier, so the flag shadows nothing.
    /// </summary>
    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var doStmt = (DoStatementSyntax)node;
        var declared = ExpressionVariableScanner.Names(doStmt.Condition);
        var condition = context.Converter.ConvertIr(doStmt.Condition);
        var body = context.Converter.ConvertStatementIr(doStmt.Statement);
        if (declared.Count == 0) return JsStatement.DoWhile(body, condition);

        var names = ExpressionVariableScanner.List(declared, context.TypeAnnotations);
        var again = JsExprWriter.Write(JsExpr.Binary(JsExpr.Identifier("$again"), "=", condition));
        return JsStatement.Headed($"for (let {names}, $again = true; $again; {again})", body);
    }

    public int Priority => 0;
}
