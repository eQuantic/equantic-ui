using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// How a total grows by one element, for <c>Sum</c> and <c>Average</c> alike: a decimal is a runtime
/// <c>Decimal</c>, whose <c>+</c> concatenates text, so it adds through its own method; anything else
/// adds with <c>+</c>.
/// </summary>
internal static class LinqAccumulation
{
    /// <summary>The total plus the element, as IR, for a callback whose body is a lambda's.</summary>
    internal static JsExpr Add(JsExpr total, JsExpr element, bool exact) =>
        exact ? JsExpr.Call(JsExpr.Member(total, "add"), element) : JsExpr.Binary(total, "+", element);

    /// <summary>The total plus the element, as a template's text.</summary>
    internal static string Add(string total, string element, bool exact) =>
        exact ? $"{total}.add({element})" : $"{total} + {element}";
}
