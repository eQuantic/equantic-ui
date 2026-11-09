using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// <c>List&lt;T&gt;</c>'s members, and those of the faces a list answers to (<c>IList&lt;T&gt;</c>,
/// <c>ICollection&lt;T&gt;</c>), over the array a list is on this side.
/// <para>
/// <c>ICollection&lt;T&gt;</c>'s own <c>Add</c> and <c>Clear</c> are the exception: an API takes that face
/// when it promises no order, and a set, a linked list or a dictionary's pairs may stand behind it when
/// the call runs, so the runtime asks the value what it is (#593), as it does for <c>Contains</c>,
/// <c>Remove</c> and <c>CopyTo</c>. They were an array's <c>push</c> and <c>splice</c>, which none of those has.
/// </para>
/// <para>
/// A member is the array's own method only where the two answer alike: <c>Add</c> is <c>push</c>,
/// <c>FindAll</c> <c>filter</c>, <c>Exists</c> <c>some</c>, <c>TrueForAll</c> <c>every</c>,
/// <c>ForEach</c> <c>forEach</c>, and <c>IndexOf</c> <c>indexOf</c> for an element compared by identity
/// that no NaN can be. Everything else goes through the runtime (<c>utils/list.ts</c>), which answers
/// as .NET does (#488, #425): <c>Sort</c> by .NET's introspective sort and the comparer it is handed,
/// where <c>sort()</c> compared the elements' text; <c>BinarySearch</c> with the complement of the
/// insertion point; <c>IndexOf</c>, <c>LastIndexOf</c> and <c>Remove</c> by the element type's equality
/// (<see cref="ElementEquality"/>), which <c>indexOf</c>'s <c>===</c> is not; <c>Find</c> and
/// <c>FindLast</c> with the element type's default; <c>FindIndex</c>'s and <c>FindLastIndex</c>'s ranges,
/// which <c>findIndex</c> took for its predicate; <c>RemoveAll</c>, which answers how many it removed
/// and once threw a ReferenceError; <c>CopyTo</c>, which writes into the array it is handed; and
/// <c>AddRange</c> and <c>InsertRange</c>, one element at a time. They were <c>push(...items)</c> and
/// <c>splice(index, 0, ...items)</c>, which hand every element to the call as an argument, and an engine
/// caps a call's arguments, so a large range threw in the browser (#1060). <c>Insert</c>, <c>RemoveAt</c>,
/// <c>RemoveRange</c> and <c>GetRange</c> check their positions as .NET does, where a <c>splice</c> or a
/// <c>slice</c> clamped them to the array or counted a negative one from its end, and take a named
/// argument in its parameter's place, where the text placed it by its position in the call (#1160).
/// </para>
/// <para>
/// A call of the array's own method is IR, so a lambda passed to it (<c>ForEach</c>, <c>Exists</c>…)
/// reaches the statement writer as an arrow whose block maps line by line (#384). Every call names its
/// arguments by the parameter each fills (<see cref="ParameterTemplate"/>).
/// </para>
/// </summary>
public class ListMethodStrategy : IExpressionIrStrategy
{
    private static readonly HashSet<string> SupportedMethods = new()
    {
        "Add", "AddRange", "Insert", "InsertRange",
        "Remove", "RemoveAt", "RemoveRange", "RemoveAll", "Clear",
        "IndexOf", "LastIndexOf", "Find", "FindIndex", "FindLast", "FindLastIndex", "FindAll",
        "Exists", "TrueForAll", "Sort", "ForEach", "GetRange", "CopyTo", "BinarySearch", "Reverse"
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!SupportedMethods.Contains(methodName))
            return false;

        // Check if it's a List<T> method via semantic model
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol { ContainingType: { } declaring } && OfAList(declaring))
            return true;

        // A Reverse no model binds is LINQ's, a reversed copy, as ReverseStrategy guesses it: a list's own,
        // in place, is only ever the one the model says it is.
        if (methodName == "Reverse") return false;

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
            return true;

        return false;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.ValueText;
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var element = context.SemanticHelper.GetType(memberAccess.Expression).GetEnumerableElementType();

        var callerIr = context.Converter.ConvertIr(memberAccess.Expression);

        // Bound: each member by the overload C# chose, its arguments in their parameters' places.
        if (method is not null && Bound(methodName, invocation, method, callerIr, element, context) is { } bound)
            return bound;

        // With no model, FindIndex's and FindLastIndex's ranges still come before the predicate, which
        // `findIndex` takes first: the runtime takes each where .NET's overloads put it (#488).
        if (method is null && methodName is "FindIndex" or "FindLastIndex" && invocation.ArgumentList.Arguments.Count is 2 or 3)
        {
            context.UsedHelpers.Add(Eq.Import);
            var helper = methodName == "FindIndex" ? Eq.ListFindIndex : Eq.ListFindLastIndex;
            return ParameterTemplate.Call(invocation.ArgumentList.Arguments.Count == 2
                    ? $"{helper}({{0}}, {{2}}, {{1}})"
                    : $"{helper}({{0}}, {{3}}, {{1}}, {{2}})",
                callerIr, invocation, null, context);
        }

        var argsIr = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertIr(a.Expression))
            .ToList();

        // A call of the array's own method, as IR, so a lambda passed to it reaches the statement
        // writer as an arrow whose block maps line by line (#384).
        if (ArrayMethod(methodName) is { } own)
            return Method(callerIr, own, argsIr);

        // The other shapes still splice their parts as text, written only here.
        var caller = JsExprWriter.Write(callerIr);
        var args = argsIr.Select(JsExprWriter.Write).ToList();
        return methodName switch
        {
            // A named argument could fill either of two positions, and no model says which.
            "AddRange" or "InsertRange" or "Insert" or "RemoveRange" or "GetRange"
                when invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null) =>
                JsExpr.Opaque(context.Unhandled(invocation, $"List.{methodName} with a named argument, which no model places")),
            "AddRange" when args.Count > 0 => Helper(context, $"{Eq.ListAddRange}({caller}, {args[0]})"),
            "Insert" when args.Count > 1 => Helper(context, $"{Eq.ListInsert}({caller}, {args[0]}, {args[1]})"),
            "InsertRange" when args.Count > 1 => Helper(context, $"{Eq.ListInsertRange}({caller}, {args[0]}, {args[1]})"),
            // An element type no model can say compares by what each value turns out to be.
            "Remove" when args.Count > 0 => Helper(context, $"{Eq.ListRemove}({caller}, {args[0]}, 'own')"),
            "RemoveAt" when args.Count > 0 => Helper(context, $"{Eq.ListRemoveAt}({caller}, {args[0]})"),
            "RemoveRange" when args.Count > 1 => Helper(context, $"{Eq.ListRemoveRange}({caller}, {args[0]}, {args[1]})"),
            "RemoveAll" when args.Count > 0 => Helper(context, $"{Eq.ListRemoveAll}({caller}, {args[0]})"),
            "Clear" => $"{caller}.splice(0)",
            "GetRange" when args.Count > 1 => Helper(context, $"{Eq.ListGetRange}({caller}, {args[0]}, {args[1]})"),
            // No model to say which overload, element type or comparer a call is: the shapes that
            // need one have none to take.
            _ => JsExpr.Opaque(context.Unhandled(invocation, $"List.{methodName}, which no model binds")),
        };
    }

    /// <summary>Whether <paramref name="add"/> is an <c>Add</c> this strategy lowers: a list's, or a
    /// list or collection interface's (<see cref="Add"/>).</summary>
    internal static bool Lowers(IMethodSymbol? add) => add is { Name: "Add", ContainingType: { } declaring } && OfAList(declaring);

    /// <summary>A list, or a list or collection interface, whose methods this strategy lowers.</summary>
    private static bool OfAList(INamedTypeSymbol type) =>
        type.ToDisplayString() is var name
        && (name.StartsWith("System.Collections.Generic.List<") || name.StartsWith("System.Collections.Generic.IList<")
            || name.StartsWith("System.Collections.Generic.ICollection<"));

    /// <summary>
    /// An <c>Add</c> this strategy lowers, as every call to it lowers: a list's is the array's <c>push</c>,
    /// and <c>ICollection&lt;T&gt;</c>'s asks the runtime, which adds as the collection the interface holds
    /// when the call runs adds (#593). An object initializer's element applied to a collection a member
    /// holds is a call to it.
    /// </summary>
    internal static JsExpr Add(IMethodSymbol? add, JsExpr list, IReadOnlyList<JsExpr> items, ConversionContext context)
    {
        if (add?.ContainingType is not { } declaring || !IsCollectionInterface(declaring)) return Method(list, "push", items);
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.CollectionAdd), [list, .. items]);
    }

    /// <summary>Whether <paramref name="type"/> is <c>ICollection&lt;T&gt;</c>, the face a set, a linked
    /// list and a dictionary's pairs answer to as readily as a list.</summary>
    private static bool IsCollectionInterface(INamedTypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.ICollection<T>";

    /// <summary>The lowering of a call the model binds, or null for the shapes every model answers alike.</summary>
    private static JsExpr? Bound(string name, InvocationExpressionSyntax invocation, IMethodSymbol method, JsExpr list,
        ITypeSymbol? element, ConversionContext context)
    {
        var count = method.Parameters.Length;
        switch (name)
        {
            // ICollection<T>'s own, for whichever collection the interface holds when the call runs (#593).
            case "Add" or "Clear" when method.ContainingType is { } declaring && IsCollectionInterface(declaring):
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call(name == "Add" ? $"{Eq.CollectionAdd}({{0}}, {{1}})" : $"{Eq.CollectionClear}({{0}})",
                    list, invocation, method, context);
            case "IndexOf" or "LastIndexOf":
            {
                var equality = ElementEquality.Of(element);
                var own = name == "IndexOf" ? "indexOf" : "lastIndexOf";
                // The array's own search compares with ===, which is the element type's equality
                // wherever that is identity and no NaN can be among the elements.
                if (count == 1 && equality is null && !MayBeNaN(element))
                    return ParameterTemplate.Call($"{{0}}.{own}({{1}})", list, invocation, method, context);
                context.UsedHelpers.Add(Eq.Import);
                var helper = name == "IndexOf" ? Eq.ListIndexOf : Eq.ListLastIndexOf;
                var range = string.Concat(Enumerable.Range(2, count - 1).Select(slot => ", {" + slot + "}"));
                var equalityArgument = equality is null && count == 1 ? "" : $", {equality ?? "false"}";
                return ParameterTemplate.Call($"{helper}({{0}}, {{1}}{equalityArgument}{range})", list, invocation, method, context);
            }
            case "Remove" when count == 1:
            {
                context.UsedHelpers.Add(Eq.Import);
                var equality = ElementEquality.Of(element);
                return ParameterTemplate.Call(equality is null ? $"{Eq.ListRemove}({{0}}, {{1}})" : $"{Eq.ListRemove}({{0}}, {{1}}, {equality})",
                    list, invocation, method, context);
            }
            case "Find" or "FindLast":
            {
                context.UsedHelpers.Add(Eq.Import);
                var fallback = DefaultValue.Of(method.ReturnType, context);
                return ParameterTemplate.Call($"{(name == "Find" ? Eq.ListFind : Eq.ListFindLast)}({{0}}, {{1}}, {fallback})",
                    list, invocation, method, context);
            }
            case "FindIndex" or "FindLastIndex":
            {
                if (count == 1) return null;
                context.UsedHelpers.Add(Eq.Import);
                var helper = name == "FindIndex" ? Eq.ListFindIndex : Eq.ListFindLastIndex;
                // (startIndex, match) and (startIndex, count, match): the match is the last parameter.
                var template = count == 2
                    ? $"{helper}({{0}}, {{2}}, {{1}})"
                    : $"{helper}({{0}}, {{3}}, {{1}}, {{2}})";
                return ParameterTemplate.Call(template, list, invocation, method, context);
            }
            case "RemoveAll":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call($"{Eq.ListRemoveAll}({{0}}, {{1}})", list, invocation, method, context);
            // The arguments as they are written, a named collection before a named index included.
            case "AddRange":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call($"{Eq.ListAddRange}({{0}}, {{1}})", list, invocation, method, context);
            case "InsertRange":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call($"{Eq.ListInsertRange}({{0}}, {{1}}, {{2}})", list, invocation, method, context);
            // Each position checked as .NET checks it, its arguments in their parameters' places (#1160).
            case "Insert":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call($"{Eq.ListInsert}({{0}}, {{1}}, {{2}})", list, invocation, method, context);
            case "RemoveAt":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call($"{Eq.ListRemoveAt}({{0}}, {{1}})", list, invocation, method, context);
            case "RemoveRange" or "GetRange":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call($"{(name == "GetRange" ? Eq.ListGetRange : Eq.ListRemoveRange)}({{0}}, {{1}}, {{2}})",
                    list, invocation, method, context);
            case "CopyTo":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call(count switch
                {
                    1 => $"{Eq.ListCopyTo}({{0}}, {{1}})",
                    2 => $"{Eq.ListCopyTo}({{0}}, {{1}}, {{2}})",
                    _ => $"{Eq.ListCopyRangeTo}({{0}}, {{1}}, {{2}}, {{3}}, {{4}})",
                }, list, invocation, method, context);
            case "Sort":
                return Sort(invocation, method, list, element, context);
            // In place, as .NET reverses a list, where it was LINQ's reversed copy, thrown away (#1076).
            case "Reverse":
                context.UsedHelpers.Add(Eq.Import);
                return ParameterTemplate.Call(count == 2 ? $"{Eq.ListReverse}({{0}}, {{1}}, {{2}})" : $"{Eq.ListReverse}({{0}})",
                    list, invocation, method, context);
            case "BinarySearch":
                return BinarySearch(invocation, method, list, element, context);
        }
        return null;
    }

    /// <summary>
    /// <c>Sort()</c>, <c>Sort(IComparer)</c>, <c>Sort(index, count, IComparer)</c> and
    /// <c>Sort(Comparison)</c>, by .NET's introspective sort: the comparison a <c>Comparison</c> is, or
    /// the order the comparer asks for (<see cref="SortOrders"/>).
    /// </summary>
    private static JsExpr Sort(InvocationExpressionSyntax invocation, IMethodSymbol method, JsExpr list,
        ITypeSymbol? element, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        if (method.Parameters is [{ Type: INamedTypeSymbol { TypeKind: TypeKind.Delegate } comparison }])
        {
            return ParameterTemplate.Call($"{Eq.ListSortBy}({{0}}, {{1}}, '{ReflectionName.Of(comparison)}')",
                list, invocation, method, context);
        }
        int? comparer = method.Parameters.Length == 0 ? null : method.Parameters.Length - 1;
        return SortOrders.Call(method.Parameters.Length == 3
                ? $"{Eq.ListSort}({{0}}, {{order}}, {{1}}, {{2}})"
                : $"{Eq.ListSort}({{0}}, {{order}})",
            list, invocation, method, comparer, element, context);
    }

    /// <summary><c>BinarySearch(item)</c>, <c>(item, IComparer)</c> and <c>(index, count, item, IComparer)</c>.</summary>
    private static JsExpr BinarySearch(InvocationExpressionSyntax invocation, IMethodSymbol method, JsExpr list,
        ITypeSymbol? element, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var parameters = method.Parameters.Length;
        int? comparer = parameters switch { 2 => 1, 4 => 3, _ => null };
        return SortOrders.Call(parameters == 4
                ? $"{Eq.ListBinarySearch}({{0}}, {{3}}, {{order}}, {{1}}, {{2}})"
                : $"{Eq.ListBinarySearch}({{0}}, {{1}}, {{order}})",
            list, invocation, method, comparer, element, context);
    }

    /// <summary>Whether an element of the type may be a NaN, which <c>indexOf</c> never finds and
    /// <c>EqualityComparer&lt;T&gt;.Default</c> does.</summary>
    private static bool MayBeNaN(ITypeSymbol? element) =>
        (element.UnwrapNullable() ?? element)?.SpecialType is SpecialType.System_Double or SpecialType.System_Single
        || element is null;

    private static JsExpr Helper(ConversionContext context, string call)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Callish(call);
    }

    /// <summary>The array method a List method is, where the call is the same call.</summary>
    private static string? ArrayMethod(string name) => name switch
    {
        "Add" => "push",
        "IndexOf" => "indexOf",
        "LastIndexOf" => "lastIndexOf",
        "FindIndex" => "findIndex",
        "FindLastIndex" => "findLastIndex",
        "FindAll" => "filter",
        "Exists" => "some",
        "TrueForAll" => "every",
        "ForEach" => "forEach",
        _ => null,
    };

    /// <summary>The array's own method, called with the arguments as they are.</summary>
    private static JsExpr Method(JsExpr caller, string name, IReadOnlyList<JsExpr> args) =>
        JsExpr.Call(JsExpr.Member(caller, name), args);

    public int Priority => 15; // Higher than InvocationStrategy (1)
}
