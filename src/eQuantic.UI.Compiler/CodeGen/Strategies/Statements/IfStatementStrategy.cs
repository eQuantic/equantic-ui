using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary><c>if</c>/<c>else</c>, an <c>else if</c> chain being an if in the else position.
/// The variables the condition declares hoist in front — see the note on scope below.</summary>
public class IfStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is IfStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var ifStmt = (IfStatementSyntax)node;
        var condition = context.Converter.ConvertIr(ifStmt.Condition);
        var ifTrue = context.Converter.ConvertStatementIr(ifStmt.Statement);
        var ifFalse = ifStmt.Else is null ? null : context.Converter.ConvertStatementIr(ifStmt.Else.Statement);
        var result = JsStatement.If(condition, ifTrue, ifFalse);

        // The SHARED scanner, not a private copy: this one walked DescendantNodesAndSelf, so a
        // condition containing a lambda (`if (rows.Any(r => r.X is T t))`) hoisted that lambda's
        // binding out into the if's own scope, where it collides with any same-named binding.
        var declarations = ExpressionVariableScanner.Declarations(ifStmt.Condition, context.TypeAnnotations);
        if (declarations.Length == 0) return result;

        // A condition's variables scope to the ENCLOSING block ("definite assignment when false":
        // `if (x is not T t) return;` leaves t usable AFTER the if, and so does
        // `if (!int.TryParse(s, out var n)) return;` — the guard idiom). Inside a
        // block parent the declarations emit as siblings; only a brace-less composite body
        // (`while (c) if (x is T t) …`) needs the wrapping block to stay one statement — and
        // there no code can follow the if anyway.
        if (ifStmt.Parent is BlockSyntax or GlobalStatementSyntax or SwitchSectionSyntax)
            return JsStatement.Hoisted(declarations, result);

        var inner = JsStatementWriter.Write(result, context.Layout, context.Depth);
        return JsStatement.Raw($"{{ {declarations}{inner} }}");
    }

    public int Priority => 0;
}
