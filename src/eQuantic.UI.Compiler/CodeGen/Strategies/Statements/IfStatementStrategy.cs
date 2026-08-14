using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class IfStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is IfStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var ifStmt = (IfStatementSyntax)node;
        var condition = context.Converter.ConvertExpression(ifStmt.Condition);
        var ifTrue = context.Converter.Convert(ifStmt.Statement);
        
        var ifFalse = "";
        if (ifStmt.Else != null)
        {
            ifFalse = " else " + context.Converter.Convert(ifStmt.Else.Statement);
        }

        var result = $"if ({condition}) {ifTrue}{ifFalse}";

        // The SHARED scanner, not a private copy: this one walked DescendantNodesAndSelf, so a
        // condition containing a lambda (`if (rows.Any(r => r.X is T t))`) hoisted that lambda's
        // binding out into the if's own scope, where it collides with any same-named binding.
        var declarations = PatternVariableScanner.Declarations(ifStmt.Condition, context.TypeAnnotations);
        if (declarations.Length > 0)
        {
            // C# pattern variables scope to the ENCLOSING block ("definite assignment when false":
            // `if (x is not T t) return;` leaves t usable AFTER the if — the guard idiom). Inside a
            // block parent the declarations emit as siblings; only a brace-less composite body
            // (`while (c) if (x is T t) …`) needs the wrapping block to stay one statement — and
            // there no code can follow the if anyway.
            if (ifStmt.Parent is BlockSyntax or GlobalStatementSyntax or SwitchSectionSyntax)
            {
                return $"{declarations}{result}";
            }
            return $"{{ {declarations}{result} }}";
        }

        return result;
    }

    public int Priority => 0;
}
