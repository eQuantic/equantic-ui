using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Calls to the static methods a user-defined operator becomes in a twin — for types declared IN
/// SOURCE, whose twin the emitter writes, and for a VOCABULARY type's conversion, whose twin the
/// runtime carries (<c>IconGlyph.fromIcons</c>). A vocabulary conversion whose twin takes the
/// operand as it is says so with <c>[ConversionPassesThrough]</c> (<c>SizeValue</c> from a number),
/// and a type from outside the SDK (<c>Index</c>) keeps the native translation; these helpers
/// answer null for both, and the caller hands the value through.
/// <para>
/// Every call of a user-defined operator or conversion is written here, so here is where the host-only
/// fence is asked of it, at the expression being converted (<see cref="ConversionContext.Converting"/>):
/// one marked <c>[ServerOnly]</c> is left out of every twin, and a conversion, implicit or spelled as a
/// cast, a compound assignment and a tuple equality called a static no twin carried
/// (<c>Amount.fromInt(7)</c>), while the strategies of a binary and a unary operator asked it each on its
/// own (Copilot's review of #944). A refused one is written as nothing more, and never as the native
/// operator a caller falls back to on null: a framework operator the runtime ships no twin for would
/// otherwise concatenate two objects.
/// </para>
/// <para>
/// And here is where the call is LIFTED where C# lifts it, an operator or a conversion applied over
/// nullable values, which the caller reads from the bound tree (#948): the static runs only over values,
/// and an absent one answers as C#'s lifted operator does (<see cref="NullableLift"/>). The static was
/// handed the null itself, and threw reading a member of it.
/// </para>
/// </summary>
public static class UserDefinedOperators
{
    /// <summary>Whether the operator's declaring type is one this compilation emits.</summary>
    public static bool IsInSource(IMethodSymbol method) =>
        method.ContainingType is { } declaring && declaring.Locations.Any(location => location.IsInSource);

    /// <summary>Whether a conversion is CALLED in the twin: always for a type this compilation
    /// declares, and for a vocabulary type unless the operator says its twin takes the operand as
    /// it is — the attribute read by NAME, because an attribute crosses assemblies by name.</summary>
    public static bool ConversionCrosses(IMethodSymbol method) =>
        IsInSource(method)
        || method.ContainingType is { } declaring && declaring.IsRuntimeProvided()
            && !method.GetAttributes().Any(a => a.AttributeClass?.Name == "ConversionPassesThroughAttribute");

    /// <summary>The static a conversion becomes on its twin — the name the emitter writes and every
    /// call site calls (<c>fromIcons</c> for <c>implicit operator IconGlyph(Icons)</c>).</summary>
    public static string ConversionName(IMethodSymbol method) => RecordTypeEmitter.ConversionNameFor(method);

    /// <summary>The conversion operator called on its operand, or null where the value passes through.
    /// <paramref name="lifted"/>, from a nullable value to a nullable value through an operator declared
    /// over the values, converts only a value, and null to null.</summary>
    public static JsExpr? Conversion(IMethodSymbol method, JsExpr operand, bool lifted, ConversionContext context)
    {
        if (Refused(method, context)) return Nothing;
        if (!ConversionCrosses(method) || method.Parameters.Length != 1) return null;
        // ONE function names a conversion, and the emitter calls the same one — they used to
        // compute it apart, and a qualified declaration made them disagree in silence.
        // The C# at the call may never name the type: `Icon(Icons.Search)` mentions no IconGlyph, and
        // an operand a call returned names none either. The call is what introduces the name.
        var call = Caller(method, RecordTypeEmitter.ConversionNameFor(method), context);
        return lifted ? NullableLift.Unary(operand, value => call([value]), context) : call([operand]);
    }

    /// <summary>The unary operator called on its operand, or null. <paramref name="lifted"/>, over a
    /// nullable value, it runs only over a value, and null answers null.</summary>
    public static JsExpr? Unary(IMethodSymbol method, string token, JsExpr operand, bool lifted, ConversionContext context)
    {
        if (Refused(method, context)) return Nothing;
        if (!IsInSource(method) || RecordTypeEmitter.UnaryOperatorMethodName(token) is not { } name) return null;
        var call = Caller(method, name, context);
        return lifted ? NullableLift.Unary(operand, value => call([value]), context) : call([operand]);
    }

    /// <summary>The binary operator called on its operands, or null. <paramref name="lifted"/>, over
    /// nullable values, it runs only over two values, and an absent one answers as C#'s lift does
    /// (<see cref="NullableLift.Operator"/>): an equality without the operator, a relation false, and any
    /// other operator null.</summary>
    public static JsExpr? Binary(IMethodSymbol method, string token, JsExpr left, JsExpr right, bool lifted,
        ConversionContext context) =>
        BinaryCall(method, token, lifted, context)?.Invoke(left, right);

    /// <summary>
    /// The binary operator as the call its caller writes over the operands it holds, decided ONCE: null
    /// where no twin carries it, nothing more where the fence refuses it, told once, and otherwise the
    /// call, lifted where <paramref name="lifted"/> says. A compound decides before it reads its place, and
    /// writes the call over what it read: asking <see cref="Binary"/> twice asked the fence and introduced
    /// the twin twice (#948's review).
    /// </summary>
    internal static Func<JsExpr, JsExpr, JsExpr>? BinaryCall(IMethodSymbol method, string token, bool lifted,
        ConversionContext context)
    {
        if (Refused(method, context)) return (_, _) => Nothing;
        if (!IsInSource(method) || RecordTypeEmitter.OperatorMethodName(token) is not { } name) return null;
        var call = Caller(method, name, context);
        return lifted
            ? (left, right) => NullableLift.Operator(token, left, right, (a, b) => call([a, b]), context)
            : (left, right) => call([left, right]);
    }

    /// <summary>The call of the static <paramref name="name"/> on the operator's twin, introduced where it
    /// is called, each operand written as it stands alone, which is the text the call has always had.</summary>
    private static Func<IReadOnlyList<JsExpr>, JsExpr> Caller(IMethodSymbol method, string name, ConversionContext context)
    {
        var twin = method.ContainingType.IntroduceTwin(context);
        return operands => JsExpr.Callish($"{twin}.{name}({string.Join(", ", operands.Select(JsExprWriter.Write))})");
    }

    /// <summary>Whether the fence refuses the operator where it is applied, telling it there.</summary>
    private static bool Refused(IMethodSymbol method, ConversionContext context) =>
        context.Converting is { } at && method.ReportIfHostOnly(at, context);

    /// <summary>What a refused operator is written as: nothing more, the build having failed.</summary>
    private static JsExpr Nothing => JsExpr.Callish("undefined");

}
