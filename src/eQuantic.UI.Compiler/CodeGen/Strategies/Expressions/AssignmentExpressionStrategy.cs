using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for assignment expressions.
/// Handles:
/// - x = y
/// - x += y
/// - (var a, var b) = (1, 2)
/// </summary>
public class AssignmentExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AssignmentExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var assignment = (AssignmentExpressionSyntax)node;

        // Deconstructing a record/struct (a plain object, not a tuple array) -> object destructuring
        // keyed by the type's Deconstruct order: `var (a, b) = point` -> `let { x: a, y: b } = point`.
        if (assignment.Left is DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax design })
        {
            var rhsType = context.SemanticHelper.GetType(assignment.Right);
            if (rhsType is { IsTupleType: false } && rhsType.DeconstructElementNames() is { } fields)
            {
                var vars = design.Variables.ToList();
                var pairs = new List<string>();
                for (var i = 0; i < vars.Count && i < fields.Count; i++)
                {
                    if (vars[i] is SingleVariableDesignationSyntax s && s.Identifier.Text != "_")
                        pairs.Add($"{fields[i]}: {s.Identifier.Text}");
                }
                var rhsObj = context.Converter.ConvertExpression(assignment.Right);
                return $"let {{ {string.Join(", ", pairs)} }} = {rhsObj}";
            }
        }

        var leftIr = context.Converter.ConvertIr(assignment.Left);
        var rightIr = context.Converter.ConvertIr(assignment.Right);
        var left = JsExprWriter.Write(leftIr);
        var right = JsExprWriter.Write(rightIr);
        var op = assignment.OperatorToken.Text;

        // Handle discard _ = ...
        if (left == "_" || left == "this._") return rightIr;

        // If it's a declaration deconstruction, prefix with 'let ' if not already handled
        if (assignment.Left is DeclarationExpressionSyntax && !left.StartsWith("let "))
        {
            return $"let {left} {op} {right}";
        }

        // COMPOUND assignment on a decimal is arithmetic, and a decimal crosses as a runtime
        // Decimal rather than a JS number — so `total += amount` concatenates their text. A running
        // money total read "R$ 01240.5089.90640.00": the seed, then each amount, glued end to end.
        // The binary form already routes here; this is the same operation spelled shorter.
        if (op.Length == 2 && op[1] == '=' && "+-*/".Contains(op[0])
            && context.SemanticHelper.GetType(assignment.Left).IsDecimal())
        {
            context.UsedHelpers.Add(Eq.Import);
            var method = op[0] switch { '+' => "add", '-' => "sub", '*' => "mul", _ => "div" };
            return JsExpr.Binary(leftIr, "=", JsExpr.Callish($"{Eq.Dec}({left}).{method}({Eq.Dec}({right}))"));
        }

        // An assignment NODE: right-associative at the loosest level, so `a = b = c` chains and
        // an assignment used as an operand is fenced by whoever places it.
        return JsExpr.Binary(leftIr, op, rightIr);
    }

    public int Priority => 10;
}

/// <summary>
/// C# 14 null-conditional assignment: <c>a?.B = v</c>, <c>a?.B += v</c>, <c>a?[i] = v</c>. The
/// PARSE shape is a conditional access whose WhenNotNull is the assignment (the target binding on
/// the left), so <see cref="ConditionalAccessStrategy"/> owns the entry point. JavaScript rejects
/// <c>?.</c> on an assignment target outright (SyntaxError — the whole emitted module dies), so
/// the guard lowers to an arrow that evaluates the receiver exactly once and assigns only when it
/// is non-null. The right side is evaluated ONLY behind the guard, which is the C# rule:
/// <c>customer?.Order = GetCurrent()</c> must not call GetCurrent() for a null customer.
/// </summary>
internal static class NullConditionalAssignment
{
    /// <summary>The guarded lowering, or null when the target shape is one this does not model —
    /// the caller reports EQ1004 instead of emitting broken JS.</summary>
    public static string? Convert(ExpressionSyntax receiver, AssignmentExpressionSyntax assignment,
        ConversionContext context, int depth = 0)
    {
        return Guarded(context.Converter.ConvertExpression(receiver), assignment, context, depth);
    }

    private static string? Guarded(string receiver, AssignmentExpressionSyntax assignment,
        ConversionContext context, int depth)
    {
        var t = depth == 0 ? "$t" : $"$t{depth}";
        var parameter = context.TypeAnnotations ? $"({t}: any)" : t;

        var target = assignment.Left switch
        {
            MemberBindingExpressionSyntax binding => $"{t}.{binding.Name.Identifier.Text.ToCamelCase()}",
            MemberAccessExpressionSyntax access when PathFromBinding(access) is { } path => $"{t}{path}",
            ElementBindingExpressionSyntax element =>
                $"{t}[{string.Join(", ", element.ArgumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)))}]",
            _ => null,
        };
        if (target is null) return null;

        var op = assignment.OperatorToken.Text;
        var right = context.Converter.ConvertExpression(assignment.Right);
        var targetType = context.SemanticHelper.GetType(assignment.Left);
        return $"({parameter} => {t} == null ? null : ({AssignBody(target, op, right, targetType, context)}))({receiver})";
    }

    /// <summary>
    /// The nested form <c>a?.b?.c = v</c>: the conditional TAIL is another conditional access
    /// carrying the assignment. Guards the outer receiver, then recurses with <c>$t.b</c>.
    /// </summary>
    public static string? ConvertNested(ExpressionSyntax receiver,
        ConditionalAccessExpressionSyntax tail, ConversionContext context, int depth = 0)
    {
        if (tail.Expression is not MemberBindingExpressionSyntax binding) return null;

        var t = depth == 0 ? "$t" : $"$t{depth}";
        var parameter = context.TypeAnnotations ? $"({t}: any)" : t;
        var innerReceiver = $"{t}.{binding.Name.Identifier.Text.ToCamelCase()}";
        var inner = tail.WhenNotNull switch
        {
            AssignmentExpressionSyntax assignment => Guarded(innerReceiver, assignment, context, depth + 1),
            ConditionalAccessExpressionSyntax deeper => NestedFrom(innerReceiver, deeper, context, depth + 1),
            _ => null,
        };
        if (inner is null) return null;

        var outer = context.Converter.ConvertExpression(receiver);
        return $"({parameter} => {t} == null ? null : {inner})({outer})";
    }

    private static string? NestedFrom(string receiver, ConditionalAccessExpressionSyntax tail,
        ConversionContext context, int depth)
    {
        if (tail.Expression is not MemberBindingExpressionSyntax binding) return null;
        var t = $"$t{depth}";
        var parameter = context.TypeAnnotations ? $"({t}: any)" : t;
        var innerReceiver = $"{t}.{binding.Name.Identifier.Text.ToCamelCase()}";
        var inner = tail.WhenNotNull switch
        {
            AssignmentExpressionSyntax assignment => Guarded(innerReceiver, assignment, context, depth + 1),
            ConditionalAccessExpressionSyntax deeper => NestedFrom(innerReceiver, deeper, context, depth + 1),
            _ => null,
        };
        return inner is null ? null : $"({parameter} => {t} == null ? null : {inner})({receiver})";
    }

    /// <summary>The member path of a <c>?.</c> tail (<c>a?.B.C</c> → <c>.b.c</c>), rooted at the
    /// binding; null when the chain roots anywhere else (a call, say — not an assignable target).</summary>
    private static string? PathFromBinding(MemberAccessExpressionSyntax access)
    {
        var name = "." + access.Name.Identifier.Text.ToCamelCase();
        return access.Expression switch
        {
            MemberBindingExpressionSyntax binding => "." + binding.Name.Identifier.Text.ToCamelCase() + name,
            MemberAccessExpressionSyntax nested when PathFromBinding(nested) is { } inner => inner + name,
            _ => null,
        };
    }

    /// <summary>The assignment itself, with the same decimal-compound routing the plain path has —
    /// a guarded `total += amount` on a decimal member must not become string glue.</summary>
    private static string AssignBody(string target, string op, string right, ITypeSymbol? targetType,
        ConversionContext context)
    {
        if (op.Length == 2 && op[1] == '=' && "+-*/".Contains(op[0]) && targetType.IsDecimal())
        {
            context.UsedHelpers.Add(Eq.Import);
            var method = op[0] switch { '+' => "add", '-' => "sub", '*' => "mul", _ => "div" };
            return $"{target} = {Eq.Dec}({target}).{method}({Eq.Dec}({right}))";
        }

        return $"{target} {op} {right}";
    }
}
