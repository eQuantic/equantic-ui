namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The single writer of JavaScript expressions. Every parenthesis in migrated output is decided
/// here, from precedence and associativity — never by a strategy guessing in an interpolated
/// string, which is how a defensive <c>(…)</c> ended up around output that never needed one and
/// how <c>f ?? g &amp;&amp; g</c> shipped as a SyntaxError.
/// </summary>
public static class JsExprWriter
{
    /// <summary>The expression standing alone — no surrounding operator, so nothing to protect it from.</summary>
    public static string Write(JsExpr expr) => Write(expr, JsPrecedence.Opaque, parentOperator: null);

    /// <summary>
    /// The expression placed where it must bind at least as tightly as <paramref name="required"/>.
    /// <see cref="JsPrecedence.Call"/> is the receiver position: <c>(a + b).toFixed()</c> needs the
    /// parentheses that <c>a.toFixed()</c> does not.
    /// </summary>
    public static string WriteIn(JsExpr expr, JsPrecedence required) => Write(expr, required, null);

    private static string Write(JsExpr expr, JsPrecedence required, string? parentOperator)
    {
        var text = Render(expr);

        // Text of unknown shape governs itself: it carries whatever parentheses the string world
        // gave it, and adding more would change output that is not ours to change yet.
        if (expr.Precedence == JsPrecedence.Opaque) return text;

        var mixes = parentOperator is not null && expr is JsBinary inner
                    && JsOperators.ForbiddenMix(parentOperator, inner.Operator);

        return expr.Precedence < required || mixes ? $"({text})" : text;
    }

    private static string Render(JsExpr expr) => expr switch
    {
        JsOpaque opaque => opaque.Text,
        JsBinary binary => RenderBinary(binary),
        JsUnary unary => RenderUnary(unary),
        JsConditional conditional => RenderConditional(conditional),
        _ => throw new InvalidOperationException($"No writer for IR node {expr.GetType().Name}."),
    };

    private static string RenderBinary(JsBinary binary)
    {
        var precedence = binary.Precedence;
        // The side the operator groups AWAY from must bind strictly tighter, or the regrouping is
        // silent: `a - (b - c)` and `a - b - c` are different sums.
        var looser = precedence + 1;
        var (left, right) = JsOperators.IsRightAssociative(binary.Operator)
            ? (looser, precedence)
            : (precedence, looser);

        return $"{Write(binary.Left, left, binary.Operator)} {binary.Operator} "
             + $"{Write(binary.Right, right, binary.Operator)}";
    }

    private static string RenderUnary(JsUnary unary)
    {
        if (!unary.IsPrefix) return $"{Write(unary.Operand, JsPrecedence.Postfix, null)}{unary.Operator}";

        var operand = Write(unary.Operand, JsPrecedence.Unary, null);

        // `-` in front of something that already starts with `-` would weld into the DECREMENT
        // operator (and `+ +x` into increment), turning a negation into a mutation.
        if (unary.Operator is "-" or "+" && operand.StartsWith(unary.Operator, StringComparison.Ordinal))
            return $"{unary.Operator}({operand})";

        // Word operators (`typeof`, `void`, `delete`) need the space their symbols do not.
        var separator = char.IsLetter(unary.Operator[^1]) ? " " : "";
        return $"{unary.Operator}{separator}{operand}";
    }

    private static string RenderConditional(JsConditional conditional)
    {
        // A condition must bind tighter than `?:` itself; the branches may be anything down to an
        // assignment, since the `?` and `:` already fence them.
        var condition = Write(conditional.Condition, JsPrecedence.Coalesce, null);
        var whenTrue = Write(conditional.WhenTrue, JsPrecedence.Assignment, null);
        var whenFalse = Write(conditional.WhenFalse, JsPrecedence.Assignment, null);
        return $"{condition} ? {whenTrue} : {whenFalse}";
    }
}
