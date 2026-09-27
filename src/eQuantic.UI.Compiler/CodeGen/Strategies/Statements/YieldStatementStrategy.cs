using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>yield return</c>/<c>yield break</c>. Inside an iterator method the converter names a buffer
/// (<see cref="ConversionContext.IteratorBuffer"/>): a yield pushes into it and a break returns it,
/// since every sequence in the emitted world is an array. Without one, the JavaScript keyword.
/// </summary>
public class YieldStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is YieldStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var yieldStmt = (YieldStatementSyntax)node;
        var buffer = context.IteratorBuffer;
        if (yieldStmt.Kind() == SyntaxKind.YieldBreakStatement)
            return JsStatement.Return(buffer is null ? null : JsExpr.Identifier(buffer));

        // What the value declares is hoisted in front, as a return's is (enclosing block).
        var declared = ExpressionVariableScanner.InFrontOf(yieldStmt, yieldStmt.Expression, context.TypeAnnotations);
        var value = context.Converter.ConvertIr(yieldStmt.Expression!);
        return JsStatement.Hoisted(declared, buffer is null
            ? JsStatement.Raw($"yield {JsExprWriter.Write(value)};")
            : JsStatement.Expression(JsExpr.Call(JsExpr.Member(JsExpr.Identifier(buffer), "push"), value)));
    }

    public int Priority => 10;
}
