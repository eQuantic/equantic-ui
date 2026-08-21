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

    public static JsStatement Const(string name, JsExpr initializer) => new JsConst(name, initializer);

    /// <summary>A statement introduced by a head the writer does not model — <c>for (…)</c>,
    /// <c>for (const x of xs)</c>, <c>label:</c> — followed by its body, laid out like any block.</summary>
    public static JsStatement Headed(string head, JsStatement body) => new JsHeaded(head, body);

    public static JsStatement Try(JsStatement body, IReadOnlyList<JsCatch> catches, JsStatement? @finally) =>
        new JsTry(body, catches, @finally);

    public static JsStatement Switch(JsExpr subject, IReadOnlyList<JsCase> cases) => new JsSwitch(subject, cases);

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

public sealed record JsConst(string Name, JsExpr Initializer) : JsStatement;

public sealed record JsHeaded(string Head, JsStatement Body) : JsStatement;

/// <summary>A catch clause: its binding text (<c>(e: any)</c>, or empty for the bare form) and block.</summary>
public sealed record JsCatch(string Binding, JsStatement Block);

public sealed record JsTry(JsStatement Body, IReadOnlyList<JsCatch> Catches, JsStatement? Finally) : JsStatement;

/// <summary>One switch section: its labels (<c>case 1</c>, <c>default</c>) and statements.</summary>
public sealed record JsCase(IReadOnlyList<string> Labels, IReadOnlyList<JsStatement> Body);

public sealed record JsSwitch(JsExpr Subject, IReadOnlyList<JsCase> Cases) : JsStatement;

public sealed record JsIf(JsExpr Condition, JsStatement Then, JsStatement? Else) : JsStatement;

public sealed record JsWhile(JsExpr Condition, JsStatement Body) : JsStatement;

public sealed record JsDoWhile(JsStatement Body, JsExpr Condition) : JsStatement;

public sealed record JsBreak(string? Label) : JsStatement;

public sealed record JsContinue(string? Label) : JsStatement;

public sealed record JsEmpty : JsStatement;
