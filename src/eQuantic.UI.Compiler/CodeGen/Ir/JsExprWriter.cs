using System.Text.RegularExpressions;

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
        // The author's parentheses. Where the surroundings are unknown (text handed to an
        // unmigrated consumer) or the inside is (opaque text), they stay exactly as written; where
        // the writer can see both, they are re-derived like any other — which is how a redundant
        // pair disappears and a needed pair is put back.
        if (expr is JsGroup group)
        {
            // A self-delimiting inside (a name, a member chain, a call, a string) reads the same in
            // every position, so its parentheses go even at the string seam. A number keeps them:
            // `(1).toString()` parses, `1.toString()` does not.
            var selfDelimiting = group.Inner.Precedence >= JsPrecedence.Call
                                 && group.Inner is not JsLiteral { IsNumeric: true };
            if (selfDelimiting) return Write(group.Inner, required, parentOperator);

            var keep = required == JsPrecedence.Opaque || group.Inner.Precedence == JsPrecedence.Opaque;
            return keep
                ? $"({Write(group.Inner, JsPrecedence.Opaque, null)})"
                : Write(group.Inner, required, parentOperator);
        }

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
        JsIdentifier identifier => identifier.Name,
        JsLiteral literal => literal.Text,
        JsMember member => $"{Receiver(member.Target)}.{member.Name}",
        JsIndex index => $"{Receiver(index.Target)}[{Write(index.IndexExpression)}]",
        JsCall call => $"{Receiver(call.Target)}({string.Join(", ", call.Arguments.Select(Argument))})",
        JsTemplate template => RenderTemplate(template),
        JsArrow arrow => RenderArrow(arrow),
        JsBinary binary => RenderBinary(binary),
        JsUnary unary => RenderUnary(unary),
        JsConditional conditional => RenderConditional(conditional),
        _ => throw new InvalidOperationException($"No writer for IR node {expr.GetType().Name}."),
    };

    private static string RenderArrow(JsArrow arrow)
    {
        var head = $"{(arrow.IsAsync ? "async " : "")}({arrow.Parameters}) => ";
        if (arrow.Block is not null) return head + arrow.Block;

        // An object literal as the body needs its own parentheses: `=> { a: 1 }` is a BLOCK with
        // a label in it, and the arrow returns undefined — the shape `Select(s => new { … })`
        // used to ship.
        var body = Write(arrow.Body!, JsPrecedence.Assignment, null);
        return body.StartsWith('{') ? $"{head}({body})" : head + body;
    }

    private static readonly Regex Hole = new(@"\{(\d)\}", RegexOptions.Compiled);

    /// <summary>
    /// Single evaluation, decided here and nowhere else. A part the template mentions more than
    /// once is bound to a parameter of an arrow and passed exactly once — unless it is a plain name
    /// or a literal, whose repeated read no program can observe, in which case it is inlined. Once
    /// any part is bound, every earlier part that could be observed is bound too, so the arguments
    /// are still evaluated in the order C# evaluates them (receiver first, then each argument).
    /// The fill is ONE pass — a part's text is never scanned for holes of its own.
    /// </summary>
    private static string RenderTemplate(JsTemplate template)
    {
        var parts = template.Parts;
        var uses = new int[parts.Count];
        foreach (Match match in Hole.Matches(template.Text))
            uses[int.Parse(match.Groups[1].Value)]++;

        var bound = new bool[parts.Count];
        for (var i = 0; i < parts.Count; i++)
            bound[i] = uses[i] > 1 && !IsInlinable(parts[i]);
        var last = Array.LastIndexOf(bound, true);
        for (var i = 0; i < last; i++)
            bound[i] |= !IsInlinable(parts[i]);

        var body = Hole.Replace(template.Text, match =>
        {
            var index = int.Parse(match.Groups[1].Value);
            return bound[index] ? "$" + index : Write(parts[index], JsPrecedence.Opaque, null);
        });
        if (last < 0) return body;

        var indexes = Enumerable.Range(0, parts.Count).Where(i => bound[i]).ToArray();
        var names = string.Join(", ", indexes.Select(i => "$" + i));
        var arguments = string.Join(", ", indexes.Select(i => Write(parts[i], JsPrecedence.Opaque, null)));
        return $"(({names}) => {body})({arguments})";
    }

    /// <summary>A read nobody can observe happening twice: a bare name (locals and parameters
    /// have no getters; <c>this</c> is a keyword) or a literal. A member read is NOT one — a
    /// property getter may count its calls.</summary>
    private static bool IsInlinable(JsExpr part) =>
        part is JsLiteral || part is JsIdentifier { Name: var name } && !name.Contains('.');

    /// <summary>A receiver must be at least call-shaped; a bare number additionally needs
    /// parentheses, because <c>1.toString()</c> reads the dot as a decimal point.</summary>
    private static string Receiver(JsExpr target) =>
        target is JsLiteral { IsNumeric: true }
            ? $"({Write(target, JsPrecedence.Opaque, null)})"
            : Write(target, JsPrecedence.Call, null);

    /// <summary>An argument is fenced by its commas; only a sequence expression would need more.</summary>
    private static string Argument(JsExpr argument) => Write(argument, JsPrecedence.Assignment, null);

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
