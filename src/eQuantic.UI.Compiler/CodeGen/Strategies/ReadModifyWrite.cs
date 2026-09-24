using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
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
/// A target made only of plain names (<c>t</c>, <c>a[i]</c>, <c>h.F</c>) keeps the plain
/// <c>target = next(target)</c> it always had, and that is legal for a reason worth writing down,
/// because a right-hand side can reassign those names: <c>a[i] += (i = 1)</c>, <c>h.F += (h =
/// other).F</c>. JavaScript fixes an assignment's target (its receiver and its key) BEFORE the
/// right-hand side runs, which is C#'s order, and every <c>next</c> reads the current value before
/// its operands, left to right. So both reads name the element C# named. A <c>next</c> that read an
/// operand first would break that, and <c>ReadModifyWriteConformanceTests</c> fails for one.
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
        Func<JsExpr, IReadOnlyList<JsExpr>, JsExpr> next, bool answerOld, ConversionContext context)
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

        return Spelled(parts, place, place, operands, next, answerOld, context);
    }

    /// <summary>
    /// The same write to a dictionary ENTRY. .NET reads it first and throws for a key that is not
    /// there, where JavaScript reads undefined and computes on: <c>m[k]++</c> made NaN and created
    /// the key, and a nullable entry's lift made null of it. So the current value is read through
    /// the guard that throws (<c>$eq.dictGet</c>), and the entry is written plainly, the receiver
    /// and the key bound once each as any target's are.
    /// </summary>
    public static JsExpr AssignEntry(JsExpr receiver, JsExpr key, IReadOnlyList<JsExpr> operands,
        Func<JsExpr, IReadOnlyList<JsExpr>, JsExpr> next, bool answerOld, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return Spelled([receiver, key], "{0}[{1}]", $"{Eq.DictGet}({{0}}, {{1}})", operands, next, answerOld, context);
    }

    /// <summary>The access when <paramref name="target"/> names a dictionary's ENTRY, which a
    /// read-modify-write reads through the guard (<see cref="AssignEntry"/>); null for any other
    /// target.</summary>
    public static ElementAccessExpressionSyntax? EntryOf(ExpressionSyntax target, ConversionContext context) =>
        target is ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } access
        && context.SemanticHelper.GetType(access.Expression).IsDictionaryLike(out _)
            ? access
            : null;

    /// <summary>The write spelled out as a template over <paramref name="parts"/>: the value read
    /// from <paramref name="read"/>, the next one written to <paramref name="place"/>, and the old
    /// one answered when asked for, bound once since no inverse recovers it.</summary>
    private static JsExpr Spelled(List<JsExpr> parts, string place, string read, IReadOnlyList<JsExpr> operands,
        Func<JsExpr, IReadOnlyList<JsExpr>, JsExpr> next, bool answerOld, ConversionContext context)
    {
        var holes = new List<JsExpr>();
        foreach (var operand in operands)
        {
            holes.Add(JsExpr.Callish("{" + parts.Count + "}"));
            parts.Add(operand);
        }

        var annotate = context.TypeAnnotations;
        var current = answerOld ? JsExpr.Identifier(Old) : JsExpr.Callish(read);
        var write = $"{place} = {JsExprWriter.Write(next(current, holes))}";
        var text = answerOld
            ? $"(({Old}{(annotate ? ": any" : "")}) => ({write}, {Old}))({read})"
            : $"({write})";
        return JsExpr.Template(text, parts, annotate);
    }

    /// <summary>A part whose second read nobody can observe: a bare name or a literal — the
    /// writer's own inlining rule, so a target it would inline needs no template at all.</summary>
    private static bool IsPlainRead(JsExpr part) =>
        part is JsLiteral || part is JsIdentifier { Name: var name } && !name.Contains('.');
}
