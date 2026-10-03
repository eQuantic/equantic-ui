using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The source of a LINQ operator, as the array its lowering calls array methods on. A LINQ shape is an
/// array method (<c>filter</c>, <c>map</c>, <c>reduce</c>, an index), and the browser holds most
/// sequences as something else: a <c>HashSet</c> as a <c>Set</c>, a dictionary and the sorted set,
/// queue, stack and linked list as the runtime's classes, a string as a string. Each threw, the string
/// on its first character. A source the model says is an array, a list or a list's face, or a LINQ
/// operator's own result, which the lowering makes an array, is read as it is; any other is read
/// through <c>$eq.linq.seq</c>, which hands an array back unchanged.
/// <para>
/// ONE place for every operator. Each strategy converted its receiver on its own, and that is twenty
/// places a rule like this one would have had to be remembered in.
/// </para>
/// </summary>
internal static class LinqSource
{
    /// <summary>The source as IR.</summary>
    internal static JsExpr Ir(ExpressionSyntax source, ConversionContext context)
    {
        var converted = context.Converter.ConvertIr(source);
        if (IsArray(source, context)) return converted;
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.LinqSeq), converted);
    }

    /// <summary>
    /// A NEW array of the source's elements, as <c>ToList</c> and <c>ToArray</c> make: the source was
    /// handed back as it was, so the copy and the original were one array, and sorting or adding to the
    /// copy changed the original. An array or a list is sliced, and anything else is read into a new
    /// array by <c>$eq.linq.toArray</c>, which copies an array too: an <c>IEnumerable</c> or an
    /// <c>IReadOnlyCollection</c> may hold a list, and <c>seq</c> hands an array back as it is. Always,
    /// even after an operator that builds its result anew: <c>Cast</c>, <c>AsEnumerable</c> and
    /// <c>DefaultIfEmpty</c> hand their source back, and a list of the ones that do not is a list.
    /// </summary>
    internal static JsExpr Copy(ExpressionSyntax source, ConversionContext context)
    {
        var converted = context.Converter.ConvertIr(source);
        // With no model to ask, the source as every operator always took it: whether it is a fresh
        // array already cannot be told, and copying every operator's result would be the price.
        if (context.SemanticHelper.GetType(source) is null) return converted;
        if (IsArray(source, context)) return JsExpr.Call(JsExpr.Member(converted, "slice"));
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.LinqToArray), converted);
    }

    /// <summary>The source as text, for the strategies that still write it.</summary>
    internal static string Text(ExpressionSyntax source, ConversionContext context) =>
        JsExprWriter.Write(Ir(source, context));

    private static bool IsArray(ExpressionSyntax source, ConversionContext context)
    {
        // With no model to ask, the array every operator always assumed.
        if (context.SemanticHelper.GetType(source) is not { } type) return true;
        if (type is IArrayTypeSymbol) return true;
        if (type is INamedTypeSymbol { OriginalDefinition: var definition }
            && (definition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
                && definition.Name is "List" or "IList" or "IReadOnlyList"
                || definition.ContainingNamespace?.ToDisplayString() == "System.Linq"
                && definition.Name is "IOrderedEnumerable" or "IGrouping"))
            return true;
        return IsOperatorResult(source, context);
    }

    /// <summary>A LINQ operator's own result, an array here, since every operator reads its source
    /// through <see cref="Ir"/>: every operator but AsEnumerable, which hands its source back.</summary>
    private static bool IsOperatorResult(ExpressionSyntax source, ConversionContext context) =>
        source is InvocationExpressionSyntax
        && context.SemanticHelper.GetSymbol(source) is IMethodSymbol { Name: not "AsEnumerable" } method
        && context.SemanticHelper.IsLinqExtension(method.ContainingType)
        && method.ReturnType is INamedTypeSymbol { Name: "IEnumerable" or "IOrderedEnumerable" };
}
