namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The expression IR: a small tree the emitter turns into JavaScript, so that structural decisions
/// — where parentheses go, above all — are made ONCE by <see cref="JsExprWriter"/> instead of by
/// every strategy in its own interpolated string.
/// <para>
/// The migration is a strangler: a strategy that has not moved yet returns its text as
/// <see cref="JsOpaque"/>, which the emitter splices verbatim, so its output is byte-identical to
/// the string world it came from. Nodes only gain meaning as producers cross over.
/// </para>
/// </summary>
public abstract record JsExpr
{
    /// <summary>How tightly this expression binds — what the emitter compares against the position
    /// it is being placed in.</summary>
    public abstract JsPrecedence Precedence { get; }

    /// <summary>Already-emitted text of unknown shape. See <see cref="JsPrecedence.Opaque"/>.</summary>
    public static JsExpr Opaque(string text) => new JsOpaque(text);

    /// <summary>Text the producer VOUCHES for as call-shaped (<c>f(x)</c>, <c>a.b(c)</c>, an IIFE)
    /// — safe as a receiver, safe as any operand, never parenthesized by anyone.</summary>
    public static JsExpr Callish(string text) => new JsOpaque(text, JsPrecedence.Call);

    public static JsExpr Binary(JsExpr left, string op, JsExpr right) => new JsBinary(left, op, right);

    public static JsExpr Prefix(string op, JsExpr operand) => new JsUnary(op, operand, IsPrefix: true);

    public static JsExpr Postfix(JsExpr operand, string op) => new JsUnary(op, operand, IsPrefix: false);

    public static JsExpr Conditional(JsExpr condition, JsExpr whenTrue, JsExpr whenFalse) =>
        new JsConditional(condition, whenTrue, whenFalse);
}

/// <summary>Emitted text the IR does not model. Carries the precedence its producer declares —
/// <see cref="JsPrecedence.Opaque"/> when nothing is claimed, which means "splice as-is".</summary>
public sealed record JsOpaque(string Text, JsPrecedence Declared = JsPrecedence.Opaque) : JsExpr
{
    public override JsPrecedence Precedence => Declared;
}

public sealed record JsBinary(JsExpr Left, string Operator, JsExpr Right) : JsExpr
{
    public override JsPrecedence Precedence => JsOperators.Precedence(Operator);
}

public sealed record JsUnary(string Operator, JsExpr Operand, bool IsPrefix) : JsExpr
{
    public override JsPrecedence Precedence => IsPrefix ? JsPrecedence.Unary : JsPrecedence.Postfix;
}

public sealed record JsConditional(JsExpr Condition, JsExpr WhenTrue, JsExpr WhenFalse) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Conditional;
}
