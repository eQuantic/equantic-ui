using System.Text;

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
        layout == JsLayout.Compact ? Compact(statement) : Pretty(statement, depth);

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

    private static string BracedPretty(JsStatement statement, int depth) =>
        NeedsBraces(statement)
            ? Pretty(JsStatement.Block(((JsStatements)statement).Statements), depth)
            : Pretty(statement, depth);

    /// <summary>A sequence of two or more is the only shape that leaks; one or none reads the same
    /// either way, and a block already carries its own braces.</summary>
    private static bool NeedsBraces(JsStatement statement) =>
        statement is JsStatements sequence && sequence.Statements.Count > 1;

    // ── pretty: one statement per line, blocks indented ────────────────────────────────────

    private static string Indent(int depth) => string.Concat(Enumerable.Repeat(Unit, depth));

    private static string Pretty(JsStatement statement, int depth) => statement switch
    {
        JsRawStatement raw => raw.Text,
        JsStatements sequence => Lines(sequence.Statements, depth),
        JsBlock block => PrettyBlock(block, depth),
        JsIf @if => $"if ({JsExprWriter.Write(@if.Condition)}) {BracedPretty(@if.Then, depth)}"
                    + (@if.Else is null ? "" : $" else {BracedPretty(@if.Else, depth)}"),
        JsHeaded headed => $"{headed.Head} {BracedPretty(headed.Body, depth)}",
        JsTry @try => $"try {Pretty(@try.Body, depth)}"
                      + string.Concat(@try.Catches.Select(c => $" catch{(c.Binding.Length == 0 ? "" : " " + c.Binding)} {Pretty(c.Block, depth)}"))
                      + (@try.Finally is null ? "" : $" finally {Pretty(@try.Finally, depth)}"),
        JsSwitch @switch => PrettySwitch(@switch, depth),
        JsWhile @while => $"while ({JsExprWriter.Write(@while.Condition)}) {BracedPretty(@while.Body, depth)}",
        JsDoWhile doWhile => $"do {BracedPretty(doWhile.Body, depth)} while ({JsExprWriter.Write(doWhile.Condition)});",
        _ => Compact(statement),
    };

    /// <summary>Statements each on their own line at this depth; an empty one takes no line.</summary>
    private static string Lines(IReadOnlyList<JsStatement> statements, int depth)
    {
        var rendered = statements.Select(s => Pretty(s, depth)).Where(text => text.Length > 0).ToList();
        return string.Join("\n" + Indent(depth), rendered);
    }

    /// <summary>Labels one level in, their statements one level further.</summary>
    private static string PrettySwitch(JsSwitch @switch, int depth)
    {
        var builder = new StringBuilder();
        builder.Append("switch (").Append(JsExprWriter.Write(@switch.Subject)).Append(") {");
        foreach (var @case in @switch.Cases)
        {
            foreach (var label in @case.Labels)
                builder.Append('\n').Append(Indent(depth + 1)).Append(label).Append(':');
            var body = Lines(@case.Body, depth + 2);
            if (body.Length > 0) builder.Append('\n').Append(Indent(depth + 2)).Append(body);
        }
        builder.Append('\n').Append(Indent(depth)).Append('}');
        return builder.ToString();
    }

    private static string PrettyBlock(JsBlock block, int depth)
    {
        var inside = Lines(block.Statements, depth + 1);
        if (inside.Length == 0) return "{}";
        var builder = new StringBuilder();
        builder.Append("{\n").Append(Indent(depth + 1)).Append(inside)
               .Append('\n').Append(Indent(depth)).Append('}');
        return builder.ToString();
    }
}
