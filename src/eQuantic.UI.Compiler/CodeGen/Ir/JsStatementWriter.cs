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
        JsIf @if => $"if ({JsExprWriter.Write(@if.Condition)}) {Compact(@if.Then)}"
                    + (@if.Else is null ? "" : $" else {Compact(@if.Else)}"),
        JsWhile @while => $"while ({JsExprWriter.Write(@while.Condition)}) {Compact(@while.Body)}",
        JsDoWhile doWhile => $"do {Compact(doWhile.Body)} while ({JsExprWriter.Write(doWhile.Condition)});",
        JsBreak { Label: null } => "break;",
        JsBreak @break => $"break {@break.Label};",
        JsContinue { Label: null } => "continue;",
        JsContinue @continue => $"continue {@continue.Label};",
        JsEmpty => "",
        _ => throw new InvalidOperationException($"No writer for IR node {statement.GetType().Name}."),
    };

    // ── pretty: one statement per line, blocks indented ────────────────────────────────────

    private static string Indent(int depth) => string.Concat(Enumerable.Repeat(Unit, depth));

    private static string Pretty(JsStatement statement, int depth) => statement switch
    {
        JsRawStatement raw => raw.Text,
        JsStatements sequence => Lines(sequence.Statements, depth),
        JsBlock block => PrettyBlock(block, depth),
        JsIf @if => $"if ({JsExprWriter.Write(@if.Condition)}) {Pretty(@if.Then, depth)}"
                    + (@if.Else is null ? "" : $" else {Pretty(@if.Else, depth)}"),
        JsWhile @while => $"while ({JsExprWriter.Write(@while.Condition)}) {Pretty(@while.Body, depth)}",
        JsDoWhile doWhile => $"do {Pretty(doWhile.Body, depth)} while ({JsExprWriter.Write(doWhile.Condition)});",
        _ => Compact(statement),
    };

    /// <summary>Statements each on their own line at this depth; an empty one takes no line.</summary>
    private static string Lines(IReadOnlyList<JsStatement> statements, int depth)
    {
        var rendered = statements.Select(s => Pretty(s, depth)).Where(text => text.Length > 0).ToList();
        return string.Join("\n" + Indent(depth), rendered);
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
