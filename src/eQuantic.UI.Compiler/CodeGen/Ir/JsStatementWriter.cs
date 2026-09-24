using System.Text;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>How statements are laid out.</summary>
public enum JsLayout
{
    /// <summary>Everything on one line, no separators — byte for byte what the string world
    /// produced, so a migrated statement strategy changes nothing until the layout is switched.</summary>
    Compact,

    /// <summary>One statement per line, blocks indented four spaces per level.</summary>
    Pretty,
}

/// <summary>
/// The single writer of JavaScript statements. Block structure, line breaks and indentation are
/// decided here from the tree; a <see cref="JsRawStatement"/> is placed verbatim — its own lines,
/// if it has several, are never re-indented, so a template literal spanning lines keeps its value.
/// </summary>
public static class JsStatementWriter
{
    private const string Unit = "    ";

    /// <summary>The statement at the given depth (a block's contents sit one level deeper).</summary>
    public static string Write(JsStatement statement, JsLayout layout, int depth = 0) =>
        layout == JsLayout.Compact ? Compact(statement) : Pretty(statement, depth).Text;

    /// <summary>
    /// The same text, and into <paramref name="marks"/> where each statement in it that carries an
    /// origin begins, counted from the start of the text (#293). The compact layout puts a whole
    /// body on one line, which no debugger steps through, so it marks nothing.
    /// </summary>
    public static string WriteMarked(JsStatement statement, JsLayout layout, int depth, List<JsLineMark> marks)
    {
        if (layout == JsLayout.Compact) return Compact(statement);
        var written = Pretty(statement, depth);
        marks.AddRange(written.Marks);
        return written.Text;
    }

    // ── compact: the string world, reproduced ──────────────────────────────────────────────

    private static string Compact(JsStatement statement) => statement switch
    {
        JsRawStatement raw => raw.Text,
        JsStatements sequence => string.Concat(sequence.Statements.Select(Compact)),
        JsBlock block => "{" + string.Concat(block.Statements.Select(Compact)) + "}",
        JsExpressionStatement expression => $"{JsExprWriter.Write(expression.Expr)};",
        JsReturn { Value: null } => "return;",
        JsReturn @return => $"return {JsExprWriter.Write(@return.Value!)};",
        JsThrow { Value: null } => "throw;",
        JsThrow @throw => $"throw {JsExprWriter.Write(@throw.Value!)};",
        JsLet let => $"let {let.Name}{let.Annotation} = {JsExprWriter.Write(let.Initializer)};",
        JsConst @const => $"const {@const.Name} = {JsExprWriter.Write(@const.Initializer)};",
        JsHeaded headed => $"{headed.Head} {BracedCompact(headed.Body)}",
        JsTry @try => $"try {Compact(@try.Body)}"
                      + string.Concat(@try.Catches.Select(c => $" catch{(c.Binding.Length == 0 ? "" : " " + c.Binding)} {Compact(c.Block)}"))
                      + (@try.Finally is null ? "" : $" finally {Compact(@try.Finally)}"),
        JsSwitch @switch => $"switch ({JsExprWriter.Write(@switch.Subject)}) {{"
                            + string.Concat(@switch.Cases.Select(c =>
                                string.Concat(c.Labels.Select(l => $" {l}:")) + string.Concat(c.Body.Select(b => " " + Compact(b)))))
                            + " }",
        JsIf @if => $"if ({JsExprWriter.Write(@if.Condition)}) {BracedCompact(@if.Then)}"
                    + (@if.Else is null ? "" : $" else {BracedCompact(@if.Else)}"),
        JsWhile @while => $"while ({JsExprWriter.Write(@while.Condition)}) {BracedCompact(@while.Body)}",
        JsDoWhile doWhile => $"do {BracedCompact(doWhile.Body)} while ({JsExprWriter.Write(doWhile.Condition)});",
        JsBreak { Label: null } => "break;",
        JsBreak @break => $"break {@break.Label};",
        JsContinue { Label: null } => "continue;",
        JsContinue @continue => $"continue {@continue.Label};",
        JsEmpty => "",
        _ => throw new InvalidOperationException($"No writer for IR node {statement.GetType().Name}."),
    };


    /// <summary>
    /// A SUBSTATEMENT — the body of a loop, the branch of an <c>if</c> — written so that several
    /// statements standing where C# needed only one still belong to the construct.
    /// <para>
    /// C# lets a body go without braces, and a pattern variable hoists a declaration in front of
    /// the statement it belongs to. Put together, the body became two statements and only the
    /// first one stayed in the loop: <c>foreach (var o in xs) items.Add(o.Flag is { Length: > 0 }
    /// flag ? … : …);</c> emitted the loop over a lone <c>let flag;</c> and left the Add behind it,
    /// which is not the same program and does not even parse (a lone `let` may not be a body).
    /// Braces here rather than at each strategy: every construct with a substatement needs the
    /// same rule, and a strategy that forgets it produces code that runs and is wrong.
    /// </para>
    /// </summary>
    private static string BracedCompact(JsStatement statement) =>
        NeedsBraces(statement) ? Compact(JsStatement.Block(((JsStatements)statement).Statements)) : Compact(statement);

    private static Written BracedPretty(JsStatement statement, int depth) =>
        NeedsBraces(statement)
            ? Pretty(JsStatement.Block(((JsStatements)statement).Statements), depth)
            : Pretty(statement, depth);

    /// <summary>A sequence of two or more is the only shape that leaks; one or none reads the same
    /// either way, and a block already carries its own braces.</summary>
    private static bool NeedsBraces(JsStatement statement) =>
        statement is JsStatements sequence && sequence.Statements.Count > 1;

    // ── pretty: one statement per line, blocks indented ────────────────────────────────────

    private static string Indent(int depth) => string.Concat(Enumerable.Repeat(Unit, depth));

    /// <summary>A statement written, with the marks of the statements in it that carry an origin,
    /// one for this statement first when it has one and writes anything.</summary>
    private static Written Pretty(JsStatement statement, int depth)
    {
        var written = statement switch
        {
            JsRawStatement raw => Written.Of(raw.Text),
            JsStatements sequence => Lines(sequence.Statements, depth),
            JsBlock block => PrettyBlock(block, depth),
            JsIf @if => new Text().Add($"if ({JsExprWriter.Write(@if.Condition)}) ").Add(BracedPretty(@if.Then, depth))
                .AddIf(@if.Else is not null, () => new Text().Add(" else ").Add(BracedPretty(@if.Else!, depth)).Done()).Done(),
            JsHeaded headed => new Text().Add($"{headed.Head} ").Add(BracedPretty(headed.Body, depth)).Done(),
            JsTry @try => PrettyTry(@try, depth),
            JsSwitch @switch => PrettySwitch(@switch, depth),
            JsWhile @while => new Text().Add($"while ({JsExprWriter.Write(@while.Condition)}) ").Add(BracedPretty(@while.Body, depth)).Done(),
            JsDoWhile doWhile => new Text().Add("do ").Add(BracedPretty(doWhile.Body, depth))
                .Add($" while ({JsExprWriter.Write(doWhile.Condition)});").Done(),
            _ => Written.Of(Compact(statement)),
        };
        return statement.Origin is { } origin && written.Text.Length > 0 ? written.MarkedAt(origin) : written;
    }

    private static Written PrettyTry(JsTry @try, int depth)
    {
        var text = new Text().Add("try ").Add(Pretty(@try.Body, depth));
        foreach (var @catch in @try.Catches)
            text.Add($" catch{(@catch.Binding.Length == 0 ? "" : " " + @catch.Binding)} ").Add(Pretty(@catch.Block, depth));
        if (@try.Finally is not null) text.Add(" finally ").Add(Pretty(@try.Finally, depth));
        return text.Done();
    }

    /// <summary>Statements each on their own line at this depth; an empty one takes no line.</summary>
    private static Written Lines(IReadOnlyList<JsStatement> statements, int depth)
    {
        var text = new Text();
        var first = true;
        foreach (var rendered in statements.Select(s => Pretty(s, depth)).Where(written => written.Text.Length > 0))
        {
            if (!first) text.Add("\n" + Indent(depth));
            text.Add(rendered);
            first = false;
        }
        return text.Done();
    }

    /// <summary>Labels one level in, their statements one level further.</summary>
    private static Written PrettySwitch(JsSwitch @switch, int depth)
    {
        var text = new Text().Add($"switch ({JsExprWriter.Write(@switch.Subject)}) {{");
        foreach (var @case in @switch.Cases)
        {
            foreach (var label in @case.Labels)
                text.Add("\n" + Indent(depth + 1) + label + ":");
            var body = Lines(@case.Body, depth + 2);
            if (body.Text.Length > 0) text.Add("\n" + Indent(depth + 2)).Add(body);
        }
        return text.Add("\n" + Indent(depth) + "}").Done();
    }

    private static Written PrettyBlock(JsBlock block, int depth)
    {
        var inside = Lines(block.Statements, depth + 1);
        if (inside.Text.Length == 0) return Written.Of("{}");
        return new Text().Add("{\n" + Indent(depth + 1)).Add(inside).Add("\n" + Indent(depth) + "}").Done();
    }

    /// <summary>Text with its marks, each counted from the start of the text.</summary>
    private sealed record Written(string Text, IReadOnlyList<JsLineMark> Marks)
    {
        public static Written Of(string text) => new(text, []);

        /// <summary>The same text, marked as beginning the statement <paramref name="origin"/>
        /// came from.</summary>
        public Written MarkedAt(SyntaxNode origin) => this with { Marks = [new JsLineMark(0, 0, origin), .. Marks] };
    }

    /// <summary>
    /// The builder the pretty layout composes with: text appended piece by piece, and the marks of
    /// every piece moved to where it landed. A mark on a piece's first line moves by the column the
    /// piece started at; one further down keeps its column, since it begins a line of its own.
    /// </summary>
    private sealed class Text
    {
        private readonly StringBuilder _text = new();
        private readonly List<JsLineMark> _marks = [];
        private int _line;
        private int _column;

        public Text Add(string piece)
        {
            _text.Append(piece);
            Advance(piece);
            return this;
        }

        public Text Add(Written piece)
        {
            foreach (var mark in piece.Marks)
                _marks.Add(mark with { Line = _line + mark.Line, Column = mark.Line == 0 ? _column + mark.Column : mark.Column });
            return Add(piece.Text);
        }

        public Text AddIf(bool condition, Func<Written> piece) => condition ? Add(piece()) : this;

        public Written Done() => new(_text.ToString(), _marks);

        private void Advance(string piece)
        {
            var lastBreak = piece.LastIndexOf('\n');
            if (lastBreak < 0)
            {
                _column += piece.Length;
                return;
            }
            _line += piece.Count(c => c == '\n');
            _column = piece.Length - lastBreak - 1;
        }
    }
}
