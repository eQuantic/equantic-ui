using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The single writer of JavaScript expressions. Every parenthesis in migrated output is decided
/// here, from precedence and associativity — never by a strategy guessing in an interpolated
/// string, which is how a defensive <c>(…)</c> ended up around output that never needed one and
/// how <c>f ?? g &amp;&amp; g</c> shipped as a SyntaxError.
/// <para>
/// Every piece it writes carries the marks of the statements inside it, which only an arrow's block
/// has, so a statement that holds a lambda can map each line of the lambda's block (#384).
/// </para>
/// </summary>
public static class JsExprWriter
{
    /// <summary>The expression standing alone — no surrounding operator, so nothing to protect it from.</summary>
    public static string Write(JsExpr expr) => Write(expr, JsPrecedence.Opaque, parentOperator: null).Text;

    /// <summary>
    /// The expression placed where it must bind at least as tightly as <paramref name="required"/>.
    /// <see cref="JsPrecedence.Call"/> is the receiver position: <c>(a + b).toFixed()</c> needs the
    /// parentheses that <c>a.toFixed()</c> does not.
    /// </summary>
    public static string WriteIn(JsExpr expr, JsPrecedence required) => Write(expr, required, null).Text;

    /// <summary>The expression standing alone, with the marks of every statement of an arrow's block
    /// in it, counted from the start of its text: what the statement writer places (#384).</summary>
    internal static JsWritten Written(JsExpr expr) => Write(expr, JsPrecedence.Opaque, null);

    private static JsWritten Write(JsExpr expr, JsPrecedence required, string? parentOperator)
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
            return keep ? Parenthesized(Written(group.Inner)) : Write(group.Inner, required, parentOperator);
        }

        var written = Render(expr);

        // Text of unknown shape governs itself: it carries whatever parentheses the string world
        // gave it, and adding more would change output that is not ours to change yet.
        if (expr.Precedence == JsPrecedence.Opaque) return written;

        var mixes = parentOperator is not null && expr is JsBinary inner
                    && JsOperators.ForbiddenMix(parentOperator, inner.Operator);

        return expr.Precedence < required || mixes ? Parenthesized(written) : written;
    }

    private static JsWritten Parenthesized(JsWritten inner) =>
        inner.Carries ? new JsWrittenBuilder().Add("(").Add(inner).Add(")").Done() : JsWritten.Of($"({inner.Text})");

    private static JsWritten Render(JsExpr expr) => expr switch
    {
        JsOpaque opaque => JsWritten.Of(opaque.Text),
        JsIdentifier identifier => JsWritten.Of(identifier.Name),
        JsLiteral literal => JsWritten.Of(literal.Text),
        JsMember member => new JsWrittenBuilder().Add(Receiver(member.Target)).Add("." + member.Name).Done(),
        JsIndex index => new JsWrittenBuilder().Add(Receiver(index.Target)).Add("[")
            .Add(Written(index.IndexExpression)).Add("]").Done(),
        JsCall call => new JsWrittenBuilder().Add(Receiver(call.Target)).Add("(")
            .AddJoined(", ", call.Arguments.Select(Argument)).Add(")").Done(),
        JsArray array => new JsWrittenBuilder().Add("[").AddJoined(", ", array.Elements.Select(Argument)).Add("]").Done(),
        JsNew creation => new JsWrittenBuilder().Add("new ").Add(Constructed(creation.Target)).Add("(")
            .AddJoined(", ", creation.Arguments.Select(Argument)).Add(")").Done(),
        JsObject literal => literal.Properties.Count == 0
            ? JsWritten.Of("{}")
            : new JsWrittenBuilder().Add("{ ").AddJoined(", ", literal.Properties.Select(Property)).Add(" }").Done(),
        JsSpread spread => new JsWrittenBuilder().Add("...").Add(Write(spread.Operand, JsPrecedence.Assignment, null)).Done(),
        JsTemplate template => RenderTemplate(template),
        JsArrow arrow => RenderArrow(arrow),
        JsArrowBlock arrow => RenderArrowBlock(arrow),
        JsBinary binary => RenderBinary(binary),
        JsUnary unary => RenderUnary(unary),
        JsConditional conditional => RenderConditional(conditional),
        _ => throw new InvalidOperationException($"No writer for IR node {expr.GetType().Name}."),
    };

    private static string ArrowHead(bool isAsync, string parameters) => $"{(isAsync ? "async " : "")}({parameters}) => ";

    private static JsWritten RenderArrow(JsArrow arrow)
    {
        // An object literal as the body needs its own parentheses: `=> { a: 1 }` is a BLOCK with
        // a label in it, and the arrow returns undefined — the shape `Select(s => new { … })`
        // used to ship.
        var body = Write(arrow.Body, JsPrecedence.Assignment, null);
        return new JsWrittenBuilder().Add(ArrowHead(arrow.IsAsync, arrow.Parameters))
            .Add(body.Text.StartsWith('{') ? Parenthesized(body) : body).Done();
    }

    /// <summary>A block, laid out once for each arrow however many times a text seam writes it:
    /// it depends on nothing but the arrow, which holds its layout and depth.</summary>
    private static readonly ConditionalWeakTable<JsArrowBlock, JsWritten> LaidOut = new();

    /// <summary>
    /// The arrow's head and its block, laid out as the lambda's place in the C# laid it out, its
    /// statements carrying their marks out with it. Where the block closes on a line of its own,
    /// the statement around the arrow takes that line back (<see cref="JsWritten.Resumes"/>).
    /// </summary>
    private static JsWritten RenderArrowBlock(JsArrowBlock arrow)
    {
        var block = LaidOut.GetValue(arrow, laid => JsStatementWriter.Written(laid.Block, laid.Layout, laid.Depth));
        var text = new JsWrittenBuilder().Add(ArrowHead(arrow.IsAsync, arrow.Parameters)).Add(block);
        return (block.Text.Contains('\n') ? text.Resume() : text).Done();
    }

    /// <summary>A hole is its part's index, of any number of digits: a call of eleven arguments has a
    /// <c>{10}</c>, which a single digit left in the text as code.</summary>
    private static readonly Regex Hole = new(@"\{(\d+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Single evaluation, decided here and nowhere else. A part the template mentions more than
    /// once is bound to a parameter of an arrow and passed exactly once — unless it is a plain name
    /// or a literal, whose repeated read no program can observe, in which case it is inlined. Once
    /// any part is bound, every earlier part that is not <see cref="IsFixed">fixed</see> is bound
    /// too, so the arguments are still evaluated in the order C# evaluates them (receiver first,
    /// then each argument). The fill is ONE pass — a part's text is never scanned for holes of its
    /// own.
    /// </summary>
    private static JsWritten RenderTemplate(JsTemplate template)
    {
        var parts = template.Parts;
        var holes = Hole.Matches(template.Text);
        var uses = new int[parts.Count];
        foreach (Match match in holes)
            uses[int.Parse(match.Groups[1].Value)]++;

        var bound = new bool[parts.Count];
        for (var i = 0; i < parts.Count; i++)
            bound[i] = uses[i] > 1 && !IsInlinable(parts[i]);
        var last = Array.LastIndexOf(bound, true);
        // A bound part runs FIRST, as the arrow's argument, and a name left inline is read later,
        // in the body, after whatever that part did. C# had read the name before it: a key whose
        // call reassigns the receiver's variable (`d.TryGetValue(Swap(), out var v)`) looked the key
        // up in the dictionary it had swapped in. So an earlier name is bound too, and only what
        // nothing can reassign stays inline.
        for (var i = 0; i < last; i++)
            bound[i] |= !IsFixed(parts[i]);

        // …and a part left INLINE runs where its hole is, after every bound one — so the inline
        // parts must meet their holes in the order C# evaluates them wherever an effect is on
        // either side of the swap: two effects would run in the other order, and a name would be
        // read on the other side of a call that reassigns it. Two names alone may swap, since
        // reading one cannot change the other. A template that fills its slots out of argument
        // order (a named argument placed in another parameter's hole) binds every part that is
        // not fixed instead.
        var inlineOrder = holes
            .Select(match => int.Parse(match.Groups[1].Value))
            .Where(index => !bound[index] && !IsFixed(parts[index]))
            .ToList();
        var swapped = inlineOrder
            .SelectMany((earlier, at) => inlineOrder.Skip(at + 1).Select(later => (Earlier: earlier, Later: later)))
            .Any(pair => pair.Earlier > pair.Later
                && !(IsInlinable(parts[pair.Earlier]) && IsInlinable(parts[pair.Later])));
        if (swapped)
        {
            for (var i = 0; i < parts.Count; i++) bound[i] |= !IsFixed(parts[i]);
            last = Array.LastIndexOf(bound, true);
        }

        var body = new JsWrittenBuilder();
        var from = 0;
        foreach (Match match in holes)
        {
            body.Add(template.Text[from..match.Index]);
            var index = int.Parse(match.Groups[1].Value);
            if (bound[index]) body.Add("$" + index);
            else body.Add(Write(parts[index], PositionOf(template.Text, match), null));
            from = match.Index + match.Length;
        }
        body.Add(template.Text[from..]);
        if (last < 0) return body.Done();

        var indexes = Enumerable.Range(0, parts.Count).Where(i => bound[i]).ToArray();
        var names = string.Join(", ", indexes.Select(i => "$" + i + (template.Annotate ? ": any" : "")));
        return new JsWrittenBuilder().Add($"(({names}) => ").Add(body.Done()).Add(")(")
            .AddJoined(", ", indexes.Select(i => Written(parts[i]))).Add(")").Done();
    }

    /// <summary>
    /// How tightly the text around a hole binds the part that fills it. Between an opening bracket
    /// or a comma and a closing bracket or a comma, the part is a whole argument or element, which
    /// only a sequence expression could break. Anywhere else an operator or a member access touches
    /// it, and a part that is not already call-shaped is parenthesized: the table's
    /// <c>(({0} * Math.PI) / 180)</c> given <c>c ? a : b</c> once multiplied only <c>b</c>.
    /// </summary>
    private static JsPrecedence PositionOf(string text, Match hole)
    {
        var before = hole.Index - 1;
        while (before >= 0 && char.IsWhiteSpace(text[before])) before--;
        var after = hole.Index + hole.Length;
        while (after < text.Length && char.IsWhiteSpace(text[after])) after++;
        var opens = before >= 0 && text[before] is '(' or ',' or '[';
        var closes = after < text.Length && text[after] is ')' or ',' or ']';
        return opens && closes ? JsPrecedence.Assignment : JsPrecedence.Call;
    }

    /// <summary>A read nobody can observe happening twice: a bare name (locals and parameters
    /// have no getters; <c>this</c> is a keyword) or a literal. A member read is NOT one — a
    /// property getter may count its calls. Internal so a template that must decide WHEN a part is
    /// read (<see cref="Strategies.DictionaryLookup"/>) asks this rule rather than keep a copy.</summary>
    internal static bool IsInlinable(JsExpr part) =>
        part is JsLiteral || part is JsIdentifier { Name: var name } && !name.Contains('.');

    /// <summary>A part whose value nothing else in the template can change: a literal, or
    /// <c>this</c> and <c>super</c>, which are keywords — and <c>super</c> is not a value an arrow
    /// could even be passed. A local's name is NOT fixed: a call among the other parts can
    /// reassign it.</summary>
    private static bool IsFixed(JsExpr part) =>
        part is JsLiteral || part is JsIdentifier { Name: "this" or "super" };

    /// <summary>A receiver must be at least call-shaped; a bare number additionally needs
    /// parentheses, because <c>1.toString()</c> reads the dot as a decimal point.</summary>
    private static JsWritten Receiver(JsExpr target) =>
        target is JsLiteral { IsNumeric: true } ? Parenthesized(Written(target)) : Write(target, JsPrecedence.Call, null);

    /// <summary>An argument is fenced by its commas; only a sequence expression would need more.</summary>
    private static JsWritten Argument(JsExpr argument) => Write(argument, JsPrecedence.Assignment, null);

    /// <summary>A property of an object literal: its key, and its value fenced as an argument is.</summary>
    private static JsWritten Property(JsProperty property) =>
        new JsWrittenBuilder().Add(property.Key + ": ").Add(Argument(property.Value)).Done();

    /// <summary>What <c>new</c> constructs: a name or a member path, or anything else in parentheses,
    /// since <c>new f()()</c> constructs <c>f</c> and calls the result.</summary>
    private static JsWritten Constructed(JsExpr target) =>
        target is JsIdentifier or JsMember or JsOpaque ? Write(target, JsPrecedence.Call, null) : Parenthesized(Written(target));

    private static JsWritten RenderBinary(JsBinary binary)
    {
        var precedence = binary.Precedence;
        // The side the operator groups AWAY from must bind strictly tighter, or the regrouping is
        // silent: `a - (b - c)` and `a - b - c` are different sums.
        var looser = precedence + 1;
        var (left, right) = JsOperators.IsRightAssociative(binary.Operator)
            ? (looser, precedence)
            : (precedence, looser);

        // A comma is written as a list's: `a, b`, never `a , b`.
        var spaced = binary.Operator == "," ? ", " : $" {binary.Operator} ";
        return new JsWrittenBuilder().Add(Write(binary.Left, left, binary.Operator)).Add(spaced)
            .Add(Write(binary.Right, right, binary.Operator)).Done();
    }

    private static JsWritten RenderUnary(JsUnary unary)
    {
        if (!unary.IsPrefix)
            return new JsWrittenBuilder().Add(Write(unary.Operand, JsPrecedence.Postfix, null)).Add(unary.Operator).Done();

        var operand = Write(unary.Operand, JsPrecedence.Unary, null);

        // `-` in front of something that already starts with `-` would weld into the DECREMENT
        // operator (and `+ +x` into increment), turning a negation into a mutation.
        if (unary.Operator is "-" or "+" && operand.Text.StartsWith(unary.Operator, StringComparison.Ordinal))
            return new JsWrittenBuilder().Add(unary.Operator).Add(Parenthesized(operand)).Done();

        // Word operators (`typeof`, `void`, `delete`) need the space their symbols do not.
        var separator = char.IsLetter(unary.Operator[^1]) ? " " : "";
        return new JsWrittenBuilder().Add(unary.Operator + separator).Add(operand).Done();
    }

    private static JsWritten RenderConditional(JsConditional conditional)
    {
        // A condition must bind tighter than `?:` itself; the branches may be anything down to an
        // assignment, since the `?` and `:` already fence them.
        return new JsWrittenBuilder()
            .Add(Write(conditional.Condition, JsPrecedence.Coalesce, null)).Add(" ? ")
            .Add(Write(conditional.WhenTrue, JsPrecedence.Assignment, null)).Add(" : ")
            .Add(Write(conditional.WhenFalse, JsPrecedence.Assignment, null)).Done();
    }
}
