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

        // `flag |= Next()` on a bool: the logical operator, both sides evaluated, the bool written
        // back — never JavaScript's `|=`, which stores a NUMBER. FIRST, ahead of the dictionary path
        // below, which returns for every compound entry write and would store the number anyway.
        // See BoolLogic.
        if (assignment.OperatorToken.Text is "|=" or "&=" or "^="
            && BoolLogic.LowerCompound(assignment, context) is { } logical)
            return logical;

        // A COMPOUND write to a dictionary entry READS it first, and .NET throws when the key is
        // not there. Emitting `map[k] op= v` would answer undefined and walk it into the
        // arithmetic; emitting the guarded read as the TARGET does not even parse. So it is
        // lowered: read through the guard, write plainly. That is only how the ENTRY is read and
        // written: the value it takes follows every rule below, as any compound target's does. A
        // template of its own had returned ahead of them, so a float entry's `+=` added doubles, a
        // decimal's glued two texts together and a byte's never wrapped.
        (JsExpr Receiver, JsExpr Key)? entry = null;
        if (assignment.Left is ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } target
            && !assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression)
            && context.SemanticHelper.GetType(target.Expression).IsDictionaryLike(out _))
        {
            entry = (context.Converter.ConvertIr(target.Expression),
                context.Converter.ConvertIr(target.ArgumentList.Arguments[0].Expression));
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

        // Every compound this strategy spells out as `target = next(target, value)` evaluates the
        // target once, as JavaScript's own `op=` and C# both do (ReadModifyWrite): the text names it
        // twice, so `values[i++] += x` would otherwise step `i` twice.
        JsExpr Compound(Func<JsExpr, JsExpr, JsExpr> next) => entry is var (receiver, key)
            ? EntryCompound(receiver, key, rightIr, next, context)
            : ReadModifyWrite.Assign(
                leftIr, [rightIr], (current, operands) => next(current, operands[0]), answerOld: false,
                context.TypeAnnotations);

        // COMPOUND assignment through a USER-DEFINED operator: `m += other` is `m = Money.opAdd(m, other)`.
        if (context.SemanticHelper.GetOperation(assignment) is Microsoft.CodeAnalysis.Operations.ICompoundAssignmentOperation
            { OperatorMethod: { } compoundMethod }
            && UserDefinedOperators.Binary(compoundMethod, op[..^1], left, right) is not null)
            return Compound((current, operand) => UserDefinedOperators.Binary(compoundMethod, op[..^1],
                JsExprWriter.Write(current), JsExprWriter.Write(operand))!);

        // COMPOUND assignment on a decimal is arithmetic on the runtime Decimal — `total +=
        // amount` emitted bare concatenates their text. A running money total read
        // "R$ 01240.5089.90640.00": the seed, then each amount, glued end to end. The target IS a
        // Decimal (typed world) and the VALUE arrives converted — the bound tree wraps a mixed
        // value in the conversion, and ValueFlow settles it like any other flow.
        if (op.Length == 2 && op[1] == '=' && "+-*/".Contains(op[0])
            && context.SemanticHelper.GetType(assignment.Left).IsDecimal())
        {
            var method = op[0] switch { '+' => "add", '-' => "sub", '*' => "mul", _ => "div" };
            return Compound((current, operand) => JsExpr.Callish(
                $"{JsExprWriter.WriteIn(current, JsPrecedence.Call)}.{method}({JsExprWriter.Write(operand)})"));
        }

        var leftType = context.SemanticHelper.GetType(assignment.Left);

        // A compound on a CHAR TARGET writes a character back: `c += 1` steps it. A char on the
        // RIGHT arrives as its code unit already — ValueFlow settles the promotion the bound tree
        // records, wherever C# applies it.
        if (op.Length >= 2 && op[^1] == '=' && op != "==" && op != "!=" && op != "<=" && op != ">=")
        {
            var binaryOp = op[..^1];
            if (leftType is { SpecialType: SpecialType.System_Char } && binaryOp is "+" or "-")
                return Compound((current, operand) => JsExpr.Callish(
                    $"String.fromCharCode({JsExprWriter.WriteIn(current, JsPrecedence.Call)}.charCodeAt(0) {binaryOp} {JsExprWriter.WriteIn(operand, JsPrecedence.Additive)})"));

            // A fixed-width target settles the compound result by its type (IntegerWidth), and a
            // float target rounds it to single precision — every one of the five, because a double
            // on the right makes it `(float)(x op y)`, and a remainder by a double is not a single.
            if (binaryOp is "+" or "-" or "*" or "<<" && IntegerWidth.Of(leftType) is { } width)
            {
                var arithmetic = ArithmeticContext.Of(assignment, context);
                if (arithmetic.IsChecked || arithmetic.ExplicitUnchecked || IntegerWidth.WrapsByDefault(width))
                {
                    return Compound((current, operand) => IntegerWidth.Settle(
                        binaryOp == "*" && width.Bits == 32 && !arithmetic.IsChecked
                            ? JsExpr.Callish($"Math.imul({JsExprWriter.Write(current)}, {JsExprWriter.Write(operand)})")
                            : JsExpr.Binary(current, binaryOp, operand),
                        leftType, arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context));
                }
            }
            if (binaryOp is "+" or "-" or "*" or "/" or "%" && SinglePrecision.Is(leftType))
                return Compound((current, operand) => SinglePrecision.Round(JsExpr.Binary(current, binaryOp, operand)));
        }

        // `x /= y` on integers is integer division, exactly like `x = x / y` — the compound form
        // used to reach JavaScript's `/=`, which divides as a double. NOT for a long target: its
        // BigInt `/` already truncates, and Math.trunc rejects a BigInt outright.
        // Built as IR, not as a template: a right-hand side that is a ternary (`x /= c ? 4 : 1`)
        // has to be fenced under the `/`, and the writer is the one that knows. The quotient goes
        // back into the TARGET's width, as C#'s `x = (T)(x / y)` does: `sbyte s = -128; s /= -1`
        // is -128, not 128.
        // `%=` joins it where the divisor could throw (#333), and so do a long's two, whose BigInt
        // operators answer a zero divisor with a RangeError of their own and long.MinValue / -1 with
        // a quotient no long holds. A divisor that settles it keeps JavaScript's own `%=`.
        var divides = op is "/=" or "%=" && leftType.IsIntegral();
        var check = divides && IntegerDivision.NeedsCheck(assignment.Right, context);
        if (divides && leftType.IsLong() && check)
            return Compound((current, operand) => IntegerDivision.OfLongs(op[..^1], current, operand, check: true, context));
        if (divides && !leftType.IsLong() && (op == "/=" || check))
        {
            var arithmetic = ArithmeticContext.Of(assignment, context);
            return Compound((current, operand) => IntegerWidth.Settle(
                IntegerDivision.OfNumbers(op[..^1], current, operand, check, context),
                leftType, arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context));
        }

        // A dictionary entry has no operator of its own to fall back on: its read is the guard.
        if (entry is not null) return Compound((current, operand) => JsExpr.Binary(current, op[..^1], operand));

        // An assignment NODE: right-associative at the loosest level, so `a = b = c` chains and
        // an assignment used as an operand is fenced by whoever places it.
        return JsExpr.Binary(leftIr, op, rightIr);
    }

    /// <summary>
    /// A compound write to a dictionary entry: the entry is read through the guard that throws for
    /// a missing key, the rule of its type computes the next value, and the entry is written
    /// plainly. The template binds the receiver and the key once each, so neither is evaluated
    /// twice, and leaves the value where C# evaluates it, after the read.
    /// </summary>
    private static JsExpr EntryCompound(JsExpr receiver, JsExpr key, JsExpr value,
        Func<JsExpr, JsExpr, JsExpr> next, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var computed = next(JsExpr.Callish($"{Eq.DictGet}({{0}}, {{1}})"), JsExpr.Opaque("{2}"));
        // Parenthesized, as a template's text must be: an assignment used as an operand
        // (`(map[k] += 1) * 2`) would otherwise take the operator into its right side.
        return JsExpr.Template($"({{0}}[{{1}}] = {JsExprWriter.Write(computed)})", [receiver, key, value],
            context.TypeAnnotations);
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
