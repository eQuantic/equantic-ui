namespace eQuantic.UI.Compiler.CodeGen.Ir;

/// <summary>
/// The expression IR: a small tree the emitter turns into JavaScript, so that structural decisions
/// — where parentheses go, above all — are made ONCE by <see cref="JsExprWriter"/> instead of by
/// every strategy in its own interpolated string.
/// <para>
/// The migration is a strangler: a strategy that has not moved yet returns its text as
/// <see cref="JsOpaque"/>, which the emitter splices verbatim, so its output is byte-identical to
/// the string world it came from. Nodes only gain meaning as producers cross over. Two
/// affordances keep that crossing cheap — a string converts to an opaque node implicitly, so a
/// migrated strategy's untouched branches still compile, and a node prints as its own JavaScript,
/// so one interpolated by mistake emits code rather than a record dump.
/// </para>
/// </summary>
public abstract record JsExpr
{
    /// <summary>How tightly this expression binds — what the emitter compares against the position
    /// it is being placed in.</summary>
    public abstract JsPrecedence Precedence { get; }

    /// <summary>The strangler seam: text is opaque. Lets a strategy migrate its signature first
    /// and its branches one at a time.</summary>
    public static implicit operator JsExpr(string text) => new JsOpaque(text);

    /// <summary>The node standing alone, as JavaScript — see <see cref="JsExprWriter.Write"/>.</summary>
    public sealed override string ToString() => JsExprWriter.Write(this);

    /// <summary>Already-emitted text of unknown shape. See <see cref="JsPrecedence.Opaque"/>.</summary>
    public static JsExpr Opaque(string text) => new JsOpaque(text);

    /// <summary>Text the producer VOUCHES for as call-shaped (<c>f(x)</c>, <c>a.b(c)</c>, an IIFE)
    /// — safe as a receiver, safe as any operand, never parenthesized by anyone.</summary>
    public static JsExpr Callish(string text) => new JsOpaque(text, JsPrecedence.Call);

    /// <summary>A name — or a resolved dotted path (<c>$eq.collections.count</c>, <c>this</c>)
    /// the writer never splits.</summary>
    public static JsExpr Identifier(string name) => new JsIdentifier(name);

    public static readonly JsExpr This = new JsIdentifier("this");

    /// <summary>A literal in its final JavaScript spelling.</summary>
    public static JsExpr Literal(string text) => new JsLiteral(text);

    public static JsExpr Member(JsExpr target, string name) => new JsMember(target, name);

    /// <summary><c>this.name</c>.</summary>
    public static JsExpr ThisMember(string name) => new JsMember(This, name);

    public static JsExpr Index(JsExpr target, JsExpr index) => new JsIndex(target, index);

    public static JsExpr Call(JsExpr target, params JsExpr[] arguments) => new JsCall(target, arguments);

    public static JsExpr Call(JsExpr target, IReadOnlyList<JsExpr> arguments) => new JsCall(target, arguments);

    /// <summary>The author's own parentheses. Kept where the writer cannot see the surroundings
    /// (text handed to an unmigrated consumer), re-derived where it can.</summary>
    public static JsExpr Group(JsExpr inner) => new JsGroup(inner);

    /// <summary>
    /// A translation written as a template over its parts — <c>{0}.normalize()</c>,
    /// <c>({0} === {1})</c> — where the WRITER owns single evaluation: a part the template uses
    /// more than once is bound exactly once (and a plain name or literal is simply inlined, a read
    /// of those cannot be observed). The template text must be self-delimiting: a call, or wrapped
    /// in its own parentheses. See <see cref="JsTemplate"/>.
    /// </summary>
    public static JsExpr Template(string template, params JsExpr[] parts) => new JsTemplate(template, parts);

    public static JsExpr Template(string template, IReadOnlyList<JsExpr> parts) => new JsTemplate(template, parts);

    /// <summary>An arrow function with an expression body. The writer parenthesizes a body that
    /// is an object literal — <c>() => ({ a: 1 })</c> — because the bare braces would read as a
    /// block, and the arrow would return undefined.</summary>
    public static JsExpr Arrow(string parameters, JsExpr body, bool isAsync = false) =>
        new JsArrow(parameters, body, null, isAsync);

    /// <summary>An arrow function with a block body, already laid out at the depth it was built.</summary>
    public static JsExpr ArrowBlock(string parameters, string block, bool isAsync = false) =>
        new JsArrow(parameters, null, block, isAsync);

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

public sealed record JsIdentifier(string Name) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Primary;
}

public sealed record JsLiteral(string Text) : JsExpr
{
    /// <summary>A signed spelling (<c>-1</c>) binds like the negation it is; everything else is
    /// primary.</summary>
    public override JsPrecedence Precedence =>
        Text.Length > 0 && Text[0] is '-' or '+' ? JsPrecedence.Unary : JsPrecedence.Primary;

    /// <summary>Whether this is a number — the one literal that cannot take a <c>.member</c>
    /// directly (<c>1.toString()</c> is a SyntaxError; <c>(1).toString()</c> is not).</summary>
    public bool IsNumeric => Text.Length > 0 && (char.IsDigit(Text[0]) || Text[0] == '.');
}

public sealed record JsMember(JsExpr Target, string Name) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Call;
}

public sealed record JsIndex(JsExpr Target, JsExpr IndexExpression) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Call;
}

public sealed record JsCall(JsExpr Target, IReadOnlyList<JsExpr> Arguments) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Call;
}

public sealed record JsGroup(JsExpr Inner) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Primary;
}

/// <summary>
/// A template over parts, with <c>{i}</c> holes. Before this, every table strategy that reused a
/// receiver spelled the single-evaluation doctrine out by hand — <c>(($s) => $s === $s.normalize())({0})</c>
/// — naming its own variables and getting the evaluation order right each time. Now the template
/// just says what it computes, <c>{0} === {0}.normalize()</c>, and the writer decides what must be
/// bound, in what order, and what can simply be inlined.
/// <para>
/// Self-delimiting by convention (a call-shaped text, or one wrapped in its own parentheses), so
/// the node is safe in any position without the writer having to parse the template.
/// </para>
/// </summary>
public sealed record JsTemplate(string Text, IReadOnlyList<JsExpr> Parts) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Call;
}

/// <summary>An arrow function: <c>(parameters) => body</c>. Exactly one of <see cref="Body"/>
/// (an expression) and <see cref="Block"/> (a block's text, laid out where it was built) is set.
/// It binds at assignment level — the loosest expression there is — so any migrated parent that
/// places one as an operand or a receiver fences it.</summary>
public sealed record JsArrow(string Parameters, JsExpr? Body, string? Block, bool IsAsync) : JsExpr
{
    public override JsPrecedence Precedence => JsPrecedence.Assignment;
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
