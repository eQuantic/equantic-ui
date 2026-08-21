namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The statement IR, the counterpart of <see cref="JsExpr"/>: a small tree the single writer
/// lays out, so that block structure, line breaks and indentation are decided once instead of by
/// every statement strategy concatenating text. Same strangler rule: a strategy that has not moved
/// returns text as <see cref="JsRawStatement"/>, spliced verbatim.
/// </summary>
public abstract record JsStatement
{
    /// <summary>The strangler seam: text is a raw statement.</summary>
    public static implicit operator JsStatement(string text) => new JsRawStatement(text);

    public static JsStatement Raw(string text) => new JsRawStatement(text);

    /// <summary>Several statements standing where one is expected — a declaration with many
    /// declarators, a hoisted pattern binding before its statement. No braces of its own.</summary>
    public static JsStatement Sequence(params JsStatement[] statements) => new JsStatements(statements);

    public static JsStatement Sequence(IReadOnlyList<JsStatement> statements) => new JsStatements(statements);

    /// <summary>Pattern-variable declarations the scanner hoists in front of a statement — text
    /// today, and nothing at all when there are none.</summary>
    public static JsStatement Hoisted(string declarations, JsStatement statement) =>
        declarations.Length == 0 ? statement : new JsStatements(new[] { Raw(declarations), statement });

    public static JsStatement Block(IReadOnlyList<JsStatement> statements) => new JsBlock(statements);

    public static JsStatement Expression(JsExpr expression) => new JsExpressionStatement(expression);

    public static JsStatement Return(JsExpr? value) => new JsReturn(value);

    public static JsStatement Throw(JsExpr? value) => new JsThrow(value);

    public static JsStatement Let(string name, string annotation, JsExpr initializer) =>
        new JsLet(name, annotation, initializer);

    public static JsStatement If(JsExpr condition, JsStatement then, JsStatement? otherwise) =>
        new JsIf(condition, then, otherwise);

    public static JsStatement While(JsExpr condition, JsStatement body) => new JsWhile(condition, body);

    public static JsStatement DoWhile(JsStatement body, JsExpr condition) => new JsDoWhile(body, condition);

    public static JsStatement Break(string? label) => new JsBreak(label);

    public static JsStatement Continue(string? label) => new JsContinue(label);

    public static readonly JsStatement Empty = new JsEmpty();
}

public sealed record JsRawStatement(string Text) : JsStatement;

public sealed record JsStatements(IReadOnlyList<JsStatement> Statements) : JsStatement;

public sealed record JsBlock(IReadOnlyList<JsStatement> Statements) : JsStatement;

public sealed record JsExpressionStatement(JsExpr Expr) : JsStatement;

public sealed record JsReturn(JsExpr? Value) : JsStatement;

public sealed record JsThrow(JsExpr? Value) : JsStatement;

/// <summary><c>let name: annotation = initializer;</c> — the annotation text already carries its
/// leading colon, or is empty.</summary>
public sealed record JsLet(string Name, string Annotation, JsExpr Initializer) : JsStatement;

public sealed record JsIf(JsExpr Condition, JsStatement Then, JsStatement? Else) : JsStatement;

public sealed record JsWhile(JsExpr Condition, JsStatement Body) : JsStatement;

public sealed record JsDoWhile(JsStatement Body, JsExpr Condition) : JsStatement;

public sealed record JsBreak(string? Label) : JsStatement;

public sealed record JsContinue(string? Label) : JsStatement;

public sealed record JsEmpty : JsStatement;
