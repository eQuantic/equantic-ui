using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Converts C# List/Collection methods to JavaScript array equivalents.
/// Handles:
/// - Add(item) -> push(item)
/// - AddRange(items) -> push(...items)
/// - Insert(index, item) -> splice(index, 0, item)
/// - Remove(item) -> splice(indexOf(item), 1)
/// - RemoveAt(index) -> splice(index, 1)
/// - RemoveRange(index, count) -> splice(index, count)
/// - RemoveAll(predicate) -> filter and reassign
/// - Clear() -> length = 0 or splice(0)
/// - IndexOf(item) -> indexOf(item)
/// - LastIndexOf(item) -> lastIndexOf(item)
/// - Find(predicate) -> find(predicate)
/// - FindIndex(predicate) -> findIndex(predicate)
/// - FindAll(predicate) -> filter(predicate)
/// - Exists(predicate) -> some(predicate)
/// - TrueForAll(predicate) -> every(predicate)
/// - Sort() -> sort()
/// - Sort(comparison) -> sort(comparison)
/// - ForEach(action) -> forEach(action)
/// - CopyTo(array) -> [...list]
/// - GetRange(index, count) -> slice(index, index + count)
/// </summary>
public class ListMethodStrategy : IConversionStrategy
{
    private static readonly HashSet<string> SupportedMethods = new()
    {
        "Add", "AddRange", "Insert", "InsertRange",
        "Remove", "RemoveAt", "RemoveRange", "RemoveAll", "Clear",
        "IndexOf", "LastIndexOf", "Find", "FindIndex", "FindLast", "FindLastIndex", "FindAll",
        "Exists", "TrueForAll", "Sort", "ForEach", "GetRange", "CopyTo", "BinarySearch"
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (!SupportedMethods.Contains(methodName))
            return false;

        // Check if it's a List<T> method via semantic model
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms)
        {
            var containingType = ms.ContainingType.ToDisplayString();
            if (containingType.StartsWith("System.Collections.Generic.List<") ||
                containingType.StartsWith("System.Collections.Generic.IList<") ||
                containingType.StartsWith("System.Collections.Generic.ICollection<"))
                return true;
        }

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToList();

        return methodName switch
        {
            "Add" => $"{caller}.push({JoinArgs(args)})",
            "AddRange" => args.Count > 0 ? $"{caller}.push(...{args[0]})" : caller,
            "Insert" => ConvertInsert(caller, args),
            "InsertRange" => ConvertInsertRange(caller, args),
            "Remove" => ConvertRemove(caller, args, context,
                context.SemanticHelper.GetType(memberAccess.Expression).GetEnumerableElementType()),
            "RemoveAt" => ConvertRemoveAt(caller, args),
            "RemoveRange" => ConvertRemoveRange(caller, args),
            "RemoveAll" => ConvertRemoveAll(caller, args),
            "Clear" => $"{caller}.splice(0)",
            "IndexOf" => $"{caller}.indexOf({JoinArgs(args)})",
            "LastIndexOf" => $"{caller}.lastIndexOf({JoinArgs(args)})",
            "Find" => $"{caller}.find({JoinArgs(args)})",
            "FindIndex" => $"{caller}.findIndex({JoinArgs(args)})",
            "FindLast" => $"{caller}.findLast({JoinArgs(args)})",
            "FindLastIndex" => $"{caller}.findLastIndex({JoinArgs(args)})",
            "FindAll" => $"{caller}.filter({JoinArgs(args)})",
            "Exists" => $"{caller}.some({JoinArgs(args)})",
            "TrueForAll" => $"{caller}.every({JoinArgs(args)})",
            "Sort" => ConvertSort(caller, args),
            "ForEach" => $"{caller}.forEach({JoinArgs(args)})",
            "GetRange" => ConvertGetRange(caller, args),
            "CopyTo" => $"[...{caller}]",
            "BinarySearch" => ConvertBinarySearch(caller, args),
            _ => $"{caller}.{methodName.ToCamelCase()}({JoinArgs(args)})"
        };
    }

    private string ConvertInsert(string caller, List<string> args)
    {
        if (args.Count >= 2)
            return $"{caller}.splice({args[0]}, 0, {args[1]})";
        return caller;
    }

    private string ConvertInsertRange(string caller, List<string> args)
    {
        if (args.Count >= 2)
            return $"{caller}.splice({args[0]}, 0, ...{args[1]})";
        return caller;
    }

    /// <summary>
    /// <c>list.Remove(item)</c>, through the runtime, which answers the bool C# does and compares as
    /// <c>EqualityComparer&lt;T&gt;.Default</c> does (#400). It assigned an index nothing declared
    /// (<c>(_idx = list.indexOf(item)) &gt;= 0 &amp;&amp; list.splice(_idx, 1)</c>), so every call threw
    /// a ReferenceError in a module, and would have answered the spliced array. The list and the item
    /// are each evaluated once, in the order C# evaluates them. A value-shaped element (a tuple, a
    /// record, a struct) compares through the structural equality <c>Contains</c> uses, so the two
    /// agree: a tuple is an array on this side, which the default comparison takes by reference (found
    /// in review, #421).
    /// </summary>
    private static string ConvertRemove(string caller, List<string> args, ConversionContext context,
        ITypeSymbol? element)
    {
        if (args.Count == 0) return caller;
        context.UsedHelpers.Add(Eq.Import);
        return Comparer(element) is { } comparer
            ? $"{Eq.ListRemove}({caller}, {args[0]}, {comparer})"
            : $"{Eq.ListRemove}({caller}, {args[0]})";
    }

    /// <summary>
    /// The comparison <c>EqualityComparer&lt;T&gt;.Default</c> makes for the element, when it is not the
    /// runtime's default: the structural one for an element compared by value, and for a
    /// <c>KeyValuePair&lt;K, V&gt;</c> one that compares each half by its own type's rule, which is what
    /// the pair's <c>Equals</c> does and what a dictionary's <c>ICollection&lt;KeyValuePair&lt;K, V&gt;&gt;.Remove</c>
    /// does with the value (found in review, #421). A pair is compared by its fields, not walked as an
    /// object: a dictionary's entries are arrays that carry <c>key</c> and <c>value</c>.
    /// </summary>
    private static string? Comparer(ITypeSymbol? element)
    {
        if (element is INamedTypeSymbol { Name: "KeyValuePair", ContainingNamespace: { } ns, TypeArguments.Length: 2 } pair
            && ns.ToDisplayString() == "System.Collections.Generic")
        {
            static string Half(ITypeSymbol half) => ComparesByValue(half) ? Eq.Equals : Eq.SameItem;
            return $"{Eq.PairComparer}({Half(pair.TypeArguments[0])}, {Half(pair.TypeArguments[1])})";
        }
        return ComparesByValue(element) ? Eq.Equals : null;
    }

    /// <summary>
    /// Whether <c>EqualityComparer&lt;T&gt;.Default</c> compares the element by value: a tuple, a record
    /// or a struct, a nullable one of those, and an anonymous type, whose <c>Equals</c> compares its
    /// members (the last two found in review, #421). Asked here and not of
    /// <c>IsStructuralValueType</c>, which <c>==</c> asks too, and an anonymous type's <c>==</c>
    /// compares references.
    /// </summary>
    private static bool ComparesByValue(ITypeSymbol? element) =>
        element.IsStructuralValueType()
        || element is { IsAnonymousType: true }
        || (element is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments[0].IsStructuralValueType());

    private string ConvertRemoveAt(string caller, List<string> args)
    {
        if (args.Count == 0) return caller;
        return $"{caller}.splice({args[0]}, 1)";
    }

    private string ConvertRemoveRange(string caller, List<string> args)
    {
        if (args.Count >= 2)
            return $"{caller}.splice({args[0]}, {args[1]})";
        return caller;
    }

    private string ConvertRemoveAll(string caller, List<string> args)
    {
        if (args.Count == 0) return caller;
        // list.RemoveAll(x => x.Active) -> filter and keep items that DON'T match
        // Returns count of removed items, but we'll just do the filter
        // This is a mutating operation, so we need a different approach
        return $"((_removed = {caller}.filter({args[0]})).length, {caller}.length = 0, {caller}.push(...{caller}.filter(_x => !({args[0]})(_x))), _removed.length)";
    }

    private string ConvertSort(string caller, List<string> args)
    {
        if (args.Count == 0)
            return $"{caller}.sort()";
        // Sort with comparison function
        return $"{caller}.sort({args[0]})";
    }

    private string ConvertGetRange(string caller, List<string> args)
    {
        if (args.Count >= 2)
            return $"{caller}.slice({args[0]}, {args[0]} + {args[1]})";
        if (args.Count == 1)
            return $"{caller}.slice({args[0]})";
        return $"[...{caller}]";
    }

    private string ConvertBinarySearch(string caller, List<string> args)
    {
        // JavaScript doesn't have built-in binary search, use findIndex as fallback
        // For sorted arrays, this is not optimal but works
        if (args.Count > 0)
            return $"{caller}.findIndex(_x => _x === {args[0]})";
        return "-1";
    }

    private string JoinArgs(List<string> args) => string.Join(", ", args);

    public int Priority => 15; // Higher than InvocationStrategy (1)
}
