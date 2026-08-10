using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Finds the variables an expression's <c>is</c> patterns BIND (<c>x is T t</c>, <c>{ Y: var y }</c>,
/// list/positional designations) so statement strategies can hoist their <c>let</c> declarations —
/// C# scopes pattern variables to the ENCLOSING block (usable after the statement: guard idioms,
/// <c>var ok = x is T t; … use(t)</c>), while the JS assignments live inside the converted condition.
/// Lambdas and local functions are NOT descended into: their bodies convert separately and their
/// bindings scope to the lambda (hoisting them out would collide same-named bindings).
/// </summary>
public static class PatternVariableScanner
{
    /// <summary>`let a; let b;` for every pattern variable bound in <paramref name="expression"/>,
    /// or an empty string when there is none.</summary>
    public static string Declarations(ExpressionSyntax? expression)
    {
        if (expression is null) return "";
        // A lambda is its OWN scope, and Walk only skips lambdas it finds as children — handed one
        // directly it walked straight into the body and hoisted a binding out of the lambda that
        // owns it, into the enclosing method.
        if (expression is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax) return "";
        var vars = new List<string>();
        // The expression ITSELF, then its children: Walk only inspects children, so a condition
        // that IS the pattern (`if (next is Accordion fresh)`) bound a name nobody declared. The
        // three original callers always passed something with the `is` nested inside it.
        if (expression is IsPatternExpressionSyntax top) Collect(top.Pattern, vars);
        Walk(expression, vars);
        // `: any`, not a bare `let`: these are assigned inside the CONDITION they are hoisted out
        // of, and TypeScript cannot see through that — an untyped slot reads as "implicitly any in
        // some locations", which is an error, where an explicit one is a decision.
        return vars.Count == 0 ? "" : string.Join(" ", vars.Select(v => $"let {v}: any;")) + " ";
    }

    private static void Walk(SyntaxNode node, List<string> vars)
    {
        foreach (var child in node.ChildNodes())
        {
            if (child is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                continue;
            // A switch expression's ARMS are the IIFE's scope and it declares them itself; its
            // GOVERNING expression is not, so the arm is skipped rather than the whole switch.
            if (child is SwitchExpressionArmSyntax) continue;
            if (child is IsPatternExpressionSyntax isPattern)
                Collect(isPattern.Pattern, vars);
            Walk(child, vars);
        }
    }

    private static void Collect(PatternSyntax pattern, List<string> vars)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax { Designation: SingleVariableDesignationSyntax d } when d.Identifier.Text != "_":
                vars.Add(d.Identifier.ValueText);
                break;
            case VarPatternSyntax { Designation: SingleVariableDesignationSyntax v } when v.Identifier.Text != "_":
                vars.Add(v.Identifier.ValueText);
                break;
            case RecursivePatternSyntax recursive:
                if (recursive.Designation is SingleVariableDesignationSyntax r && r.Identifier.Text != "_")
                    vars.Add(r.Identifier.ValueText);
                if (recursive.PositionalPatternClause is { } positional)
                    foreach (var sub in positional.Subpatterns) Collect(sub.Pattern, vars);
                if (recursive.PropertyPatternClause is { } property)
                    foreach (var sub in property.Subpatterns) Collect(sub.Pattern, vars);
                break;
            case ListPatternSyntax list:
                foreach (var p in list.Patterns) Collect(p, vars);
                break;
            case SlicePatternSyntax { Pattern: { } slice }:
                Collect(slice, vars);
                break;
            case BinaryPatternSyntax binary:
                Collect(binary.Left, vars);
                Collect(binary.Right, vars);
                break;
            case UnaryPatternSyntax unary:
                Collect(unary.Pattern, vars);
                break;
            case ParenthesizedPatternSyntax paren:
                Collect(paren.Pattern, vars);
                break;
        }
    }
}
