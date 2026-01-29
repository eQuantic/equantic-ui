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

        // Fallback heuristic when no semantic info
        if (context.SemanticModel == null || symbol == null)
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
            "Remove" => ConvertRemove(caller, args),
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
            _ => $"{caller}.{ToCamelCase(methodName)}({JoinArgs(args)})"
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

    private string ConvertRemove(string caller, List<string> args)
    {
        if (args.Count == 0) return caller;
        // list.Remove(item) -> list.splice(list.indexOf(item), 1)
        return $"((_idx = {caller}.indexOf({args[0]})) >= 0 && {caller}.splice(_idx, 1))";
    }

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

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 15; // Higher than InvocationStrategy (1)
}
