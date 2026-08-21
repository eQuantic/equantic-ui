using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// <c>using (resource) body</c> → a block that binds the resource, runs the body in a
/// <c>try</c>, and disposes in the <c>finally</c> — through the runtime's <c>dispose()</c>
/// contract, guarded, since the value may not have one.
/// </summary>
public class UsingStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is UsingStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var usingStmt = (UsingStatementSyntax)node;
        string resourceVar;
        JsExpr init;
        if (usingStmt.Declaration != null)
        {
            // using (var x = new X()) { ... }
            var variable = usingStmt.Declaration.Variables.First();
            resourceVar = variable.Identifier.Text;
            init = variable.Initializer != null
                ? context.Converter.ConvertIr(variable.Initializer.Value)
                : JsExpr.Literal("null");
        }
        else
        {
            // using (expr) ... — the expression is captured under a temporary so it can be disposed.
            resourceVar = "_disposable_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            init = context.Converter.ConvertIr(usingStmt.Expression!);
        }

        var body = context.Converter.ConvertStatementIr(usingStmt.Statement);
        var resource = JsExpr.Identifier(resourceVar);
        var dispose = JsStatement.If(
            JsExpr.Opaque($"{resourceVar} && typeof {resourceVar}.dispose === 'function'"),
            JsStatement.Block(new[] { JsStatement.Expression(JsExpr.Call(JsExpr.Member(resource, "dispose"))) }),
            null);

        return JsStatement.Block(new[]
        {
            JsStatement.Const(resourceVar, init),
            JsStatement.Try(body is JsBlock ? body : JsStatement.Block(new[] { body }),
                Array.Empty<JsCatch>(),
                JsStatement.Block(new[] { dispose })),
        });
    }

    public int Priority => 0;
}
