using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A read-modify-write the compiler spells out as <c>target = next(target)</c>: every increment and
/// compound assignment JavaScript's own <c>++</c> or <c>op=</c> would get wrong — a width that
/// wraps, a float that rounds, a char that steps its code unit, a Decimal, a user-defined operator.
/// JavaScript's own forms evaluate their target ONCE, and so does C#; the spelled-out one names it
/// twice. So the target's receiver and key become template parts, and the writer binds whichever
/// of them could be observed (<see cref="JsTemplate"/>): <c>values[i++] += 1.5f</c> steps <c>i</c>
/// once, and <c>Get().Value++</c> calls <c>Get</c> once. The right-hand side is a part too, never
/// spliced text, so nothing it contains is mistaken for a hole.
/// <para>
/// A target made only of plain names (<c>t</c>, <c>this.count</c>) reads the same twice, and keeps
/// the plain <c>target = next(target)</c> it always had.
/// </para>
/// </summary>
public static class ReadModifyWrite
{
    private const string Old = "$old";

    /// <summary>
    /// The write. <paramref name="next"/> builds the new value from the CURRENT one and from the
    /// <paramref name="operands"/>, each handed to it as a placeholder to use exactly once.
    /// <paramref name="answerOld"/> makes the expression answer the value BEFORE the write — a
    /// postfix step whose value is read — which no inverse recovers once the step has wrapped or
    /// rounded, so it is bound once.
    /// </summary>
    public static JsExpr Assign(JsExpr target, IReadOnlyList<JsExpr> operands,
        Func<JsExpr, IReadOnlyList<JsExpr>, JsExpr> next, bool answerOld, bool annotate)
    {
        var parts = new List<JsExpr>();
        string place;
        switch (target)
        {
            case JsIndex index:
                parts.Add(index.Target);
                parts.Add(index.IndexExpression);
                place = "{0}[{1}]";
                break;
            case JsMember member:
                parts.Add(member.Target);
                place = "{0}." + member.Name;
                break;
            default:
                place = JsExprWriter.Write(target);
                break;
        }

        if (!answerOld && parts.All(IsPlainRead))
            return JsExpr.Binary(target, "=", next(target, operands));

        var holes = new List<JsExpr>();
        foreach (var operand in operands)
        {
            holes.Add(JsExpr.Callish("{" + parts.Count + "}"));
            parts.Add(operand);
        }

        var current = answerOld ? JsExpr.Identifier(Old) : JsExpr.Callish(place);
        var write = $"{place} = {JsExprWriter.Write(next(current, holes))}";
        var text = answerOld
            ? $"(({Old}{(annotate ? ": any" : "")}) => ({write}, {Old}))({place})"
            : $"({write})";
        return JsExpr.Template(text, parts, annotate);
    }

    /// <summary>A part whose second read nobody can observe: a bare name or a literal — the
    /// writer's own inlining rule, so a target it would inline needs no template at all.</summary>
    private static bool IsPlainRead(JsExpr part) =>
        part is JsLiteral || part is JsIdentifier { Name: var name } && !name.Contains('.');
}
