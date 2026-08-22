using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Calls to the static methods a user-defined operator becomes in an emitted twin — for types
/// declared IN SOURCE, whose twin the emitter writes. A framework type with an operator
/// (<c>SizeValue</c>, <c>Index</c>) has a hand-written twin that IS its primitive, and its
/// operators pass the value through; these helpers answer null for it, and the caller keeps
/// the native translation.
/// </summary>
public static class UserDefinedOperators
{
    /// <summary>Whether the operator's declaring type is one this compilation emits.</summary>
    public static bool IsInSource(IMethodSymbol method) =>
        method.ContainingType is { } declaring && declaring.Locations.Any(location => location.IsInSource);

    /// <summary>The conversion operator called on its operand, or null where the value passes through.</summary>
    public static JsExpr? Conversion(IMethodSymbol method, string operand)
    {
        if (!IsInSource(method) || method.Parameters.Length != 1) return null;
        var declaring = method.ContainingType;
        var toSelf = SymbolEqualityComparer.Default.Equals(method.ReturnType, declaring);
        var other = toSelf ? method.Parameters[0].Type : method.ReturnType;
        var name = RecordTypeEmitter.ConversionMethodName(Display(other), from: toSelf);
        return JsExpr.Callish($"{declaring.Name}.{name}({operand})");
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

    /// <summary>The keyword form a symbol displays (`int`, not Int32), the text the emitter read.</summary>
    private static string Display(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
}
