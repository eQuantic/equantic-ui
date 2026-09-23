using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Calls to the static methods a user-defined operator becomes in a twin — for types declared IN
/// SOURCE, whose twin the emitter writes, and for a VOCABULARY type's conversion, whose twin the
/// runtime carries (<c>IconGlyph.fromIcons</c>). A vocabulary conversion whose twin takes the
/// operand as it is says so with <c>[ConversionPassesThrough]</c> (<c>SizeValue</c> from a number),
/// and a type from outside the SDK (<c>Index</c>) keeps the native translation; these helpers
/// answer null for both, and the caller hands the value through.
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

    /// <summary>The conversion operator called on its operand, or null where the value passes through.</summary>
    public static JsExpr? Conversion(IMethodSymbol method, string operand, ConversionContext context)
    {
        if (!ConversionCrosses(method) || method.Parameters.Length != 1) return null;
        // ONE function names a conversion, and the emitter calls the same one — they used to
        // compute it apart, and a qualified declaration made them disagree in silence.
        var name = RecordTypeEmitter.ConversionNameFor(method);
        // A vocabulary type is imported from the runtime, and the C# at the call never names it:
        // `Icon(Icons.Search)` mentions no IconGlyph. The call is what introduces the name.
        if (!IsInSource(method)) context.UsedRuntimeTypes.Add(method.ContainingType.Name);
        return JsExpr.Callish($"{method.ContainingType.Name}.{name}({operand})");
    }

    /// <summary>The unary operator called on its operand, or null.</summary>
    public static JsExpr? Unary(IMethodSymbol method, string token, string operand)
    {
        if (!IsInSource(method) || RecordTypeEmitter.UnaryOperatorMethodName(token) is not { } name) return null;
        return JsExpr.Callish($"{method.ContainingType.Name}.{name}({operand})");
    }

    /// <summary>The binary operator called on its operands, or null.</summary>
    public static JsExpr? Binary(IMethodSymbol method, string token, string left, string right)
    {
        if (!IsInSource(method) || RecordTypeEmitter.OperatorMethodName(token) is not { } name) return null;
        return JsExpr.Callish($"{method.ContainingType.Name}.{name}({left}, {right})");
    }

}
