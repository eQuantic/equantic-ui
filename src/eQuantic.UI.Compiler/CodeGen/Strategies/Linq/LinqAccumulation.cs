using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// How a total grows by one element, for <c>Sum</c> and <c>Average</c> alike, by the type of the values
/// added, named as <c>$eq.linq.average</c> is told it (<c>Mean</c> in <c>utils/linq.ts</c>), though a sum adds in
/// its own inline reduce: an <c>int</c> and a <c>long</c> through the runtime's checked
/// addition, since <c>Enumerable.Sum</c> checks them whatever the context of the call (#1808); a decimal
/// through its own method, since it is a runtime <c>Decimal</c>, whose <c>+</c> concatenates text; anything
/// else with <c>+</c>. A nullable's null is passed over as the zero of its type, which leaves the total as it
/// was: a null added to a BigInt or a Decimal threw a TypeError.
/// </summary>
internal static class LinqAccumulation
{
    /// <summary>The total plus the element, as IR, for a callback whose body is a lambda's.</summary>
    internal static JsExpr Add(JsExpr total, JsExpr element, string mean, bool nullable)
    {
        if (nullable) element = JsExpr.Binary(element, "??", JsExpr.Literal(Zero(mean)));
        return mean switch
        {
            "int" => JsExpr.Call(JsExpr.Identifier(Eq.AddInt), total, element),
            "long" => JsExpr.Call(JsExpr.Identifier(Eq.AddLong), total, element),
            "decimal" => JsExpr.Call(JsExpr.Member(total, "add"), element),
            _ => JsExpr.Binary(total, "+", element),
        };
    }

    /// <summary>The total plus the element, as a template's text.</summary>
    internal static string Add(string total, string element, string mean, bool nullable)
    {
        if (nullable) element = $"({element} ?? {Zero(mean)})";
        return mean switch
        {
            "int" => $"{Eq.AddInt}({total}, {element})",
            "long" => $"{Eq.AddLong}({total}, {element})",
            "decimal" => $"{total}.add({element})",
            _ => $"{total} + {element}",
        };
    }

    /// <summary>The zero a total of the type starts at: a BigInt for a long, a <c>Decimal</c> for a decimal.</summary>
    internal static string Zero(string mean) => mean switch
    {
        "long" => "0n",
        "decimal" => $"{Eq.Dec}(0)",
        _ => "0",
    };

    /// <summary>Whether adding the type's values reads the runtime: a decimal's zero is one of its values, and
    /// an int's and a long's addition is its checked one.</summary>
    internal static bool ReadsTheRuntime(string mean) => mean is "int" or "long" or "decimal";

    /// <summary>
    /// How the runtime adds the values an overload of <c>Sum</c> or <c>Average</c> means, and whether they are
    /// nullable: the selector's result type where the overload takes one, and the source's element type
    /// otherwise, which every overload without a selector names outright. Null for any other type, which no
    /// overload of .NET 10 means.
    /// </summary>
    internal static (string Mean, bool Nullable)? ValuesOf(IMethodSymbol method)
    {
        var values = method.Parameters.FirstOrDefault(parameter => parameter.Name == "selector")?.Type is INamedTypeSymbol
        {
            TypeArguments: [_, var result],
        }
            ? result
            : ((method.ReducedFrom ?? method).Parameters.FirstOrDefault()?.Type).GetEnumerableElementType();
        var mean = values.UnwrapNullable()?.SpecialType switch
        {
            SpecialType.System_Int32 => "int",
            SpecialType.System_Double => "number",
            SpecialType.System_Int64 => "long",
            SpecialType.System_Single => "single",
            SpecialType.System_Decimal => "decimal",
            _ => null,
        };
        return mean is null ? null : (mean, values.IsNullableValue());
    }
}
