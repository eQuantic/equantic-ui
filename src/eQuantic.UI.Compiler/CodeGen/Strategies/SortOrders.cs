using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// The comparer a sort or a binary search is handed, as the runtime's <c>SortOrder</c>
/// (<c>utils/sort.ts</c>): how it compares, and which of .NET's two sort helpers it is.
/// <para>
/// The default comparer of a type, <c>Comparer&lt;T&gt;.Default</c>, compares by the type's ordering
/// (<see cref="ValueOrdering"/>, the table <c>Max</c>, <c>Min</c> and every sorted collection read), with
/// the helper .NET picks for it: <c>GenericArraySortHelper</c> for a type that is
/// <c>IComparable&lt;T&gt;</c> (<c>'comparable'</c>, or <c>'real'</c> for a double or a float, whose NaNs
/// go first), and <c>ArraySortHelper</c> for any other (<c>'comparer'</c>: an enum, a
/// <c>Nullable&lt;T&gt;</c>). A type that is not comparable at all sorts as .NET's does: it throws once
/// two elements are compared. A comparer handed in is translated where it crosses
/// (<see cref="CollectionComparerExtensions.SortOrderAskedFor"/>) and refused with EQ2007 where it does
/// not; a type whose values have no faithful order here (a tuple, a Guid, <c>object</c>) is refused
/// with EQ1004.
/// </para>
/// </summary>
internal static class SortOrders
{
    /// <summary>
    /// The order of a call, from the comparer its parameter <paramref name="comparer"/> is handed (none:
    /// the default), and the call's template over its parameters (<see cref="ParameterTemplate"/>) with
    /// the order written where <c>{order}</c> stands; after a refusal was reported, the call's own C# text,
    /// as <see cref="ConversionContext.Unhandled"/> writes one (<see cref="Refused"/>). The comparer's
    /// own argument is converted only where the order reads it as a value: a <c>StringComparer</c>'s
    /// property and <c>Comparer&lt;T&gt;.Default</c> are no value this side.
    /// </summary>
    internal static JsExpr Call(string template, JsExpr? receiver, InvocationExpressionSyntax invocation, IMethodSymbol method,
        int? comparer, ITypeSymbol? element, ConversionContext context)
    {
        ArgumentSyntax? comparerArgument = null;
        IOperation? value = null;
        string order;
        if (comparer is { } ordinal
            && ParameterTemplate.Filling(invocation, method, ordinal) is { } argument
            && context.SemanticHelper.GetOperation(argument.Expression) is { } operation)
        {
            comparerArgument = argument;
            // The default a null comparer stands for at run time. A comparer that is no default can
            // order a type that has no order here; only one that turns out null meets that fallback.
            var fallback = Default(element, invocation, context, report: operation.AsksForTheDefaultOrder());
            if (fallback is null) return Refused(invocation);
            var hole = ParameterTemplate.WrittenFor(invocation.ArgumentList.Arguments, method)[ordinal] + (receiver is null ? 0 : 1);
            if (operation.SortOrderAskedFor(fallback, "{" + hole + "}", context) is not { } asked) return Refused(invocation);
            order = asked.Order;
            value = asked.Value;
        }
        else if (Default(element, invocation, context, report: true) is { } fallback)
        {
            order = fallback;
        }
        else
        {
            return Refused(invocation);
        }
        // The comparer's hole is numbered by where it was written, so the template's {order} is bound
        // as it stands; the rest of the template names parameters, which ParameterTemplate points at
        // their arguments.
        return ParameterTemplate.Call(template, receiver, invocation, method, context,
            argument => argument != comparerArgument
                ? context.Converter.ConvertIr(argument.Expression)
                : value is null ? JsExpr.Literal("null") : context.Converter.ConvertIr((ExpressionSyntax)value.Syntax),
            ("{order}", order));
    }

    /// <summary>
    /// A refused call, its diagnostic already reported, written as its own C# text, as
    /// <see cref="ConversionContext.Unhandled"/> writes one: text that fails where it runs, where a value
    /// standing in for the call (an <c>undefined</c>) would have run as a sort that sorted nothing.
    /// </summary>
    internal static JsExpr Refused(InvocationExpressionSyntax invocation) => JsExpr.Opaque(invocation.ToString());

    /// <summary>
    /// The default comparer's order for an element type. Where the type has no faithful order here, it
    /// is reported when <paramref name="report"/> says the call relies on it, and null; and otherwise it
    /// is the order a type .NET cannot compare has, which throws once asked, as a null comparer handed in
    /// at run time would leave the call with nothing to order by here.
    /// </summary>
    internal static string? Default(ITypeSymbol? element, SyntaxNode at, ConversionContext context, bool report)
    {
        context.UsedHelpers.Add(Eq.Import);
        if (element is not null && ValueOrdering.Of(element) is { } ordering)
            return $"{Eq.SortOrder}({ordering}, '{Kind(element)}')";
        if (element is not null && !MayCompare(element)) return $"{Eq.SortOrder}(null)";
        if (!report) return $"{Eq.SortOrder}(null)";
        context.Unhandled(at, element is null
            ? "a sort or a search whose element type no model can say"
            : $"a sort or a search over {element.ToDisplayString()}, whose values have no faithful order here");
        return null;
    }

    /// <summary>The helper .NET's default comparer sorts a type with.</summary>
    private static string Kind(ITypeSymbol element)
    {
        if (element.SpecialType is SpecialType.System_Double or SpecialType.System_Single) return "real";
        return element.AllInterfaces.Any(contract => contract.ContainingNamespace?.ToDisplayString() == "System"
                && contract.OriginalDefinition.MetadataName == "IComparable`1"
                && SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], element))
            ? "comparable"
            : "comparer";
    }

    /// <summary>
    /// Whether .NET's default comparer may order a value of the type: one that is comparable, or one
    /// whose value may be (<c>object</c>, an interface, a type parameter). A class or a struct that is
    /// neither makes .NET's comparer throw once two of its values are compared.
    /// </summary>
    private static bool MayCompare(ITypeSymbol element) =>
        element.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType
        || element.TypeKind is TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Dynamic
        || element.AllInterfaces.Any(contract => contract.ContainingNamespace?.ToDisplayString() == "System"
            && contract.OriginalDefinition.MetadataName is "IComparable" or "IComparable`1");
}
