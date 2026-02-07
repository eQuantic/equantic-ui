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
            var declarations = string.Join(" ", patternVars.Select(v => $"let {v};"));
            // Wrap in block to keep scope clean and allow usage in 'else' contexts
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
            case RecursivePatternSyntax recursive:
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
