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

        // Scan for pattern variables in condition
        var patternVars = GetPatternVariables(ifStmt.Condition);
        if (patternVars.Any())
        {
            // `: any`, not a bare `let`: the slot is assigned INSIDE the condition it is hoisted out
            // of, and TypeScript cannot see through that — an untyped one is an error rather than
            // merely a missing type. What the value IS stays checked where it comes from.
            var declarations = string.Join(" ", patternVars.Select(v => $"let {v}: any;"));
            // C# pattern variables scope to the ENCLOSING block ("definite assignment when false":
            // `if (x is not T t) return;` leaves t usable AFTER the if — the guard idiom). Inside a
            // block parent the declarations emit as siblings; only a brace-less composite body
            // (`while (c) if (x is T t) …`) needs the wrapping block to stay one statement — and
            // there no code can follow the if anyway.
            if (ifStmt.Parent is BlockSyntax or GlobalStatementSyntax or SwitchSectionSyntax)
            {
                return $"{declarations} {result}";
            }
            return $"{{ {declarations} {result} }}";
        }

        return result;
    }

    private List<string> GetPatternVariables(ExpressionSyntax expression)
    {
        var vars = new List<string>();
        
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            if (node is IsPatternExpressionSyntax isPattern)
            {
                CollectPatternVariables(isPattern.Pattern, vars);
            }
        }
        
        return vars;
    }

    private void CollectPatternVariables(PatternSyntax pattern, List<string> vars)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax decl:
                if (decl.Designation is SingleVariableDesignationSyntax single && single.Identifier.Text != "_")
                {
                    vars.Add(single.Identifier.Text);
                }
                break;
            case VarPatternSyntax { Designation: SingleVariableDesignationSyntax varDesig }
                when varDesig.Identifier.Text != "_":
                // `var x` anywhere in the pattern (e.g. `{ Y: var y }`, `(0, var b)`, `[var a, ..]`).
                vars.Add(varDesig.Identifier.Text);
                break;
            case RecursivePatternSyntax recursive:
                if (recursive.Designation is SingleVariableDesignationSyntax recDesig && recDesig.Identifier.Text != "_")
                    vars.Add(recDesig.Identifier.Text);
                if (recursive.PositionalPatternClause != null)
                {
                    foreach (var sub in recursive.PositionalPatternClause.Subpatterns)
                        CollectPatternVariables(sub.Pattern, vars);
                }
                if (recursive.PropertyPatternClause != null)
                {
                    foreach (var sub in recursive.PropertyPatternClause.Subpatterns)
                        CollectPatternVariables(sub.Pattern, vars);
                }
                break;
            case SlicePatternSyntax { Pattern: { } slicePat }:
                CollectPatternVariables(slicePat, vars);
                break;
             case BinaryPatternSyntax binary:
                CollectPatternVariables(binary.Left, vars);
                CollectPatternVariables(binary.Right, vars);
                break;
             case UnaryPatternSyntax unary:
                CollectPatternVariables(unary.Pattern, vars);
                break;
             case ParenthesizedPatternSyntax paren:
                CollectPatternVariables(paren.Pattern, vars);
                break;
             case ListPatternSyntax list:
                foreach (var p in list.Patterns)
                    CollectPatternVariables(p, vars);
                break;
        }
    }

    public int Priority => 0;
}
