using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>while (cond) body</c>.</summary>
public class WhileStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is WhileStatementSyntax;
    }

    /// <summary>
    /// A condition that declares variables (<c>while (int.TryParse(next, out var n))</c>) becomes
    /// <c>for (let n; cond;)</c>. Roslyn scopes them to the loop and .NET gives every iteration its
    /// own, so a closure made in the body keeps its iteration's value; a <c>let</c> in front of the
    /// <c>while</c> was one slot for all of them (.NET 12, JavaScript 0), and two sibling loops
    /// declaring the same name, which C# allows, were a duplicate declaration. A <c>for</c>'s own
    /// <c>let</c> is copied for each iteration, which is exactly .NET's rule.
    /// </summary>
    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var whileStmt = (WhileStatementSyntax)node;
        var declared = ExpressionVariableScanner.Names(whileStmt.Condition);
        var condition = context.Converter.ConvertIr(whileStmt.Condition);
        var body = context.Converter.ConvertStatementIr(whileStmt.Statement);
        if (declared.Count == 0) return JsStatement.While(condition, body);

        var names = ExpressionVariableScanner.List(declared, context.TypeAnnotations);
        return JsStatement.Headed($"for (let {names}; {JsExprWriter.Write(condition)};)", body);
    }

    public int Priority => 0;
}
