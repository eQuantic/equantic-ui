namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// JavaScript operator precedence, tightest LAST. The emitter derives parentheses from this
/// instead of each strategy hand-writing them — which is how <c>f ?? g &amp;&amp; g</c> once shipped
/// verbatim: C# needs no parentheses there (<c>&amp;&amp;</c> binds tighter than <c>??</c>), and
/// JavaScript REFUSES the unparenthesized mix outright, so the whole bundle stopped parsing.
/// </summary>
public enum JsPrecedence
{
    /// <summary>
    /// Text the IR cannot reason about — the output of a strategy not yet migrated. It is spliced
    /// VERBATIM and never parenthesized, which is exactly what the string world did before the IR
    /// existed: the strangler boundary costs nothing and changes nothing until a producer moves
    /// across it. Every such fragment either parenthesizes itself or is call-shaped already.
    /// </summary>
    Opaque = 0,

    Sequence = 1,
    Assignment = 2,
    Conditional = 3,
    Coalesce = 4,
    LogicalOr = 5,
    LogicalAnd = 6,
    BitwiseOr = 7,
    BitwiseXor = 8,
    BitwiseAnd = 9,
    Equality = 10,
    Relational = 11,
    Shift = 12,
    Additive = 13,
    Multiplicative = 14,
    Exponent = 15,
    Unary = 16,
    Postfix = 17,

    /// <summary>Call, member access and <c>new</c> with arguments — what a receiver must be.</summary>
    Call = 18,

    /// <summary>Identifiers, literals, and anything already wrapped in its own brackets.</summary>
    Primary = 19,
}

/// <summary>The precedence and associativity of a JavaScript binary operator.</summary>
public static class JsOperators
{
    /// <summary>Where an operator binds. Unknown operators sit at <see cref="JsPrecedence.Sequence"/>
    /// — the loosest real level — so an unrecognised one over-parenthesizes rather than mis-parses.</summary>
    public static JsPrecedence Precedence(string op) => op switch
    {
        "," => JsPrecedence.Sequence,
        "=" or "+=" or "-=" or "*=" or "/=" or "%=" or "**=" or "<<=" or ">>=" or ">>>="
            or "&=" or "^=" or "|=" or "&&=" or "||=" or "??=" => JsPrecedence.Assignment,
        "??" => JsPrecedence.Coalesce,
        "||" => JsPrecedence.LogicalOr,
        "&&" => JsPrecedence.LogicalAnd,
        "|" => JsPrecedence.BitwiseOr,
        "^" => JsPrecedence.BitwiseXor,
        "&" => JsPrecedence.BitwiseAnd,
        "==" or "!=" or "===" or "!==" => JsPrecedence.Equality,
        "<" or ">" or "<=" or ">=" or "instanceof" or "in" => JsPrecedence.Relational,
        "<<" or ">>" or ">>>" => JsPrecedence.Shift,
        "+" or "-" => JsPrecedence.Additive,
        "*" or "/" or "%" => JsPrecedence.Multiplicative,
        "**" => JsPrecedence.Exponent,
        _ => JsPrecedence.Sequence,
    };

    /// <summary>Right-associative operators: assignment and exponentiation. Everything else in the
    /// table groups to the left, so <c>a - b - c</c> is <c>(a - b) - c</c>.</summary>
    public static bool IsRightAssociative(string op) =>
        op == "**" || Precedence(op) == JsPrecedence.Assignment;

    /// <summary>
    /// The one place JavaScript is STRICTER than precedence alone: <c>??</c> may not sit next to
    /// <c>&amp;&amp;</c> or <c>||</c> without parentheses, in either direction. Not a style rule —
    /// the unparenthesized mix is a SyntaxError, so the file never loads.
    /// </summary>
    public static bool ForbiddenMix(string outer, string inner) =>
        (outer == "??" && inner is "&&" or "||") || (outer is "&&" or "||" && inner == "??");
}
