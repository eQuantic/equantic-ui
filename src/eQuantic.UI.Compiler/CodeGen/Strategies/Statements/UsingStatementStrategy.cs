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
public class UsingStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is UsingStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var usingStmt = (UsingStatementSyntax)node;
        string resourceVar;
        JsExpr init;
        // What the resource's expression declares (`using (var r = Open(out var size))`) is the
        // statement's own — Roslyn scopes it there — and the block this lowering opens is exactly
        // that scope.
        var declared = ExpressionVariableScanner.Declarations(
            usingStmt.Declaration?.Variables.First().Initializer?.Value ?? usingStmt.Expression, context.TypeAnnotations);
        if (usingStmt.Declaration != null)
        {
            // using (var x = new X()) { ... }
            var variable = usingStmt.Declaration.Variables.First();
            resourceVar = variable.Identifier.Text.ToJsIdentifier();
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
        var dispose = UsingLowering.Dispose(resourceVar, usingStmt.AwaitKeyword.Value != null);

        var statements = new List<JsStatement>();
        if (declared.Length > 0) statements.Add(JsStatement.Raw(declared.TrimEnd()));
        statements.Add(JsStatement.Const(resourceVar, init));
        statements.Add(JsStatement.Try(body is JsBlock ? body : JsStatement.Block(new[] { body }),
            null,
            JsStatement.Block(new[] { dispose })));
        return JsStatement.Block(statements);
    }

    public int Priority => 0;
}
