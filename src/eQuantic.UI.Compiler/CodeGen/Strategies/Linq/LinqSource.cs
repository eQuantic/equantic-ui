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
    internal static JsExpr Ir(ExpressionSyntax source, ConversionContext context) =>
        Read(source, ReceiverParameter(source, context), context);

    /// <summary>
    /// The source as IR, read through <c>seq</c> by the name the operator gives it, which a null one is
    /// refused by: <c>source</c> for most, <c>first</c> for a <c>Zip</c>'s receiver, <c>second</c> for a
    /// <c>Concat</c>'s argument. Every one was refused as <c>source</c> (#569).
    /// </summary>
    private static JsExpr Read(ExpressionSyntax source, string? parameter, ConversionContext context)
    {
        var before = context.Diagnostics.Count;
        var converted = context.Converter.ConvertIr(source);
        if (IsArray(source, context)) return converted;
        if (context.Diagnostics.Count == before)
            IterableTwin.FenceEnumeration(context.SemanticHelper.GetType(source), source, context);
        context.UsedHelpers.Add(Eq.Import);
        return parameter is null or "source"
            ? JsExpr.Call(JsExpr.Identifier(Eq.LinqSeq), converted)
            : JsExpr.Call(JsExpr.Identifier(Eq.LinqSeq), converted, JsExpr.Literal(JsStringLiteral.Quote(parameter)));
    }

    /// <summary>
    /// The source of an operator the runtime walks itself (<c>utils/query.ts</c>, #685), handed over as it
    /// is: the runtime reads it one element at a time, a list through its version, a string by its chars.
    /// What the walk cannot enumerate is refused here, as it is where the source is read.
    /// </summary>
    internal static JsExpr Lazy(ExpressionSyntax source, ConversionContext context)
    {
        var before = context.Diagnostics.Count;
        var converted = context.Converter.ConvertIr(source);
        if (!IsArray(source, context) && context.Diagnostics.Count == before)
            IterableTwin.FenceEnumeration(context.SemanticHelper.GetType(source), source, context);
        return converted;
    }

    /// <summary>An argument of an operator the runtime walks: a sequence as it is (<see cref="Lazy"/>), and
    /// any other argument as <see cref="Argument"/> converts it.</summary>
    internal static JsExpr LazyArgument(ArgumentSyntax argument, InvocationExpressionSyntax invocation, ConversionContext context) =>
        context.SemanticHelper.GetOperation(invocation) is Microsoft.CodeAnalysis.Operations.IInvocationOperation call
        && LinqArguments.Written(argument, invocation, context) is var written
        && call.Arguments.FirstOrDefault(bound => bound.Syntax == written)?.Parameter is { } bound
        && bound.OriginalDefinition.Type is INamedTypeSymbol parameter
        && IsSequence(parameter)
            ? Lazy(argument.Expression, context)
            : context.Converter.ConvertIr(argument.Expression);

    /// <summary>Whether <paramref name="source"/> is a query the runtime walks lazily: the result of an operator
    /// <see cref="LinqTableStrategy"/> hands to <c>$eq.query</c> (#685). An operator that answers from it walks
    /// it too, and stops where .NET's stops, where reading it as an array ran every callback upstream first.</summary>
    internal static bool IsQuery(ExpressionSyntax source, ConversionContext context) =>
        source is InvocationExpressionSyntax
        && context.SemanticHelper.GetSymbol(source) is IMethodSymbol method
        && context.SemanticHelper.IsLinqExtension(method.ContainingType)
        && (LinqTableStrategy.Lazy(method.Name, method.ReducedFrom is not null ? method.Parameters.Length : method.Parameters.Length - 1) is not null
            || Deferred(method.Name));

    /// <summary>
    /// The deferred operators the runtime has no walk of its own for: run when their query is walked
    /// (<see cref="Defer"/>), the source read then, as .NET runs them, rather than when they are called
    /// (#685). Their other arguments are evaluated when they are called, as C# evaluates them.
    /// </summary>
    internal static bool Deferred(string name) => name is "Union" or "Intersect" or "Except" or "DistinctBy" or "Chunk"
        or "Join" or "GroupJoin" or "SkipLast" or "TakeLast" or "ExceptBy" or "IntersectBy" or "UnionBy"
        or "Order" or "OrderDescending" or "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending"
        or "Distinct" or "GroupBy" or "Reverse" or "OfType" or "DefaultIfEmpty" or "Zip";

    /// <summary>
    /// <paramref name="template"/>, whose <c>{0}</c> is the source, run when its query is walked: wrapped in
    /// <c>$eq.query.defer</c>, which refuses a null source when the operator is called, by the name its
    /// receiver binds to, and reads it into a new array each time the query is walked, for the template to
    /// answer from (#685). The source goes in as <see cref="Lazy"/> reads it, and every other part is
    /// evaluated when the operator is called, as C# evaluates an argument: the writer binds a part a
    /// function of the template reads.
    /// </summary>
    internal static string Defer(string template, ExpressionSyntax source, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var parameter = context.TypeAnnotations ? "$src: any" : "$src";
        var name = ReceiverParameter(source, context) is { } bound and not "source" ? $", {JsStringLiteral.Quote(bound)}" : "";
        return $"{Eq.Query}.defer(({parameter}) => {template.Replace("{0}", "$src")}, {{0}}{name})";
    }

    /// <summary>The name of the parameter a receiver binds to: the extension's own first parameter.</summary>
    private static string? ReceiverParameter(ExpressionSyntax source, ConversionContext context) =>
        source.Parent is MemberAccessExpressionSyntax access && access.Expression == source
        && access.Parent is InvocationExpressionSyntax invocation && invocation.Expression == access
        && context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { ReducedFrom.Parameters: [var receiver, ..] }
            ? receiver.Name
            : null;

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
        var before = context.Diagnostics.Count;
        var converted = context.Converter.ConvertIr(source);
        // With no model to ask, the source as every operator always took it: whether it is a fresh
        // array already cannot be told, and copying every operator's result would be the price.
        if (context.SemanticHelper.GetType(source) is { } sequenceType)
        {
            if (IsArray(source, context)) return JsExpr.Call(JsExpr.Member(converted, "slice"));
            if (context.Diagnostics.Count == before)
                IterableTwin.FenceEnumeration(sequenceType, source, context);
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.LinqToArray), converted);
        }
        return converted;
    }

    /// <summary>
    /// An argument of a LINQ call, read as a source when the parameter it binds to is DECLARED a
    /// sequence (<c>Intersect</c>'s, <c>Except</c>'s, <c>Zip</c>'s second, <c>Concat</c>'s, or the span
    /// an array's <c>SequenceEqual</c> binds through <c>MemoryExtensions</c>): a set or a queue there
    /// was handed to <c>includes</c> or spread as a string by code point, as the receiver was before it
    /// went through here. The declaration decides, not the type the call substitutes: <c>Append</c>'s
    /// element is a <c>TSource</c>, which is a sequence when the source holds sequences, and a queue
    /// appended there was read into an array instead of being the element. Any other argument
    /// converts as it always did. Under a null-conditional the call's arguments are copies, so the bound
    /// argument is found by the one written in the tree (<see cref="LinqArguments.Written"/>): a string
    /// zipped there was walked by code point.
    /// </summary>
    internal static JsExpr Argument(ArgumentSyntax argument, InvocationExpressionSyntax invocation, ConversionContext context) =>
        context.SemanticHelper.GetOperation(invocation) is Microsoft.CodeAnalysis.Operations.IInvocationOperation call
        && LinqArguments.Written(argument, invocation, context) is var written
        && call.Arguments.FirstOrDefault(bound => bound.Syntax == written)?.Parameter is { } bound
        && bound.OriginalDefinition.Type is INamedTypeSymbol parameter
        && IsSequence(parameter)
            ? Read(argument.Expression, bound.Name, context)
            : context.Converter.ConvertIr(argument.Expression);

    /// <summary>A parameter a LINQ call reads elements from: an <c>IEnumerable&lt;T&gt;</c>, or a span.</summary>
    private static bool IsSequence(INamedTypeSymbol parameter) =>
        parameter.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
        || parameter.OriginalDefinition is { MetadataName: "ReadOnlySpan`1" or "Span`1", ContainingNamespace: var space }
            && space.ToDisplayString() == "System";

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
                && definition.Name is "IGrouping"))
            return true;
        return IsOperatorResult(source, context);
    }

    /// <summary>A LINQ operator's own result, an array here, since every operator reads its source
    /// through <see cref="Ir"/>: every operator but AsEnumerable, which hands its source back, and the ones
    /// the runtime walks itself (<see cref="LinqTableStrategy.Lazy"/>) or runs when walked
    /// (<see cref="Deferred"/>), which answer a query.</summary>
    private static bool IsOperatorResult(ExpressionSyntax source, ConversionContext context) =>
        source is InvocationExpressionSyntax invocation
        && context.SemanticHelper.GetSymbol(source) is IMethodSymbol { Name: not "AsEnumerable" } method
        && context.SemanticHelper.IsLinqExtension(method.ContainingType)
        && method.ReturnType is INamedTypeSymbol { Name: "IEnumerable" or "IOrderedEnumerable" }
        && LinqTableStrategy.Lazy(method.Name, method.ReducedFrom is not null ? method.Parameters.Length : method.Parameters.Length - 1) is null
        && !Deferred(method.Name);
}
