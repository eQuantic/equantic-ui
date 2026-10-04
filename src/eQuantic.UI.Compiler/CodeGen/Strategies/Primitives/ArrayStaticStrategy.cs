using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for Array static methods.
/// Handles:
/// - Array.Sort, Array.IndexOf, Array.LastIndexOf, Array.Find, Array.FindLast, Array.FindIndex and
///   Array.FindLastIndex through the runtime (utils/list.ts), as .NET answers them (#488, #425)
/// - Array.Reverse(array) -> array.reverse()
/// - Array.FindAll(array, predicate) -> array.filter(predicate)
/// - Array.Exists(array, predicate) -> array.some(predicate)
/// - Array.TrueForAll(array, predicate) -> array.every(predicate)
/// </summary>
public class ArrayStaticStrategy : IConversionStrategy
{
    private static readonly HashSet<string> SupportedMethods = new()
    {
        "Sort", "Reverse", "Find", "FindLast", "FindIndex", "FindLastIndex", "FindAll",
        "IndexOf", "LastIndexOf", "Exists", "TrueForAll", "Clear", "Resize"
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (!SupportedMethods.Contains(methodName)) return false;

        // The receiver must BE System.Array — a user type merely named Array must not route here.
        return context.ReceiverIsType(memberAccess.Expression,
            named => named.SpecialType == SpecialType.System_Array,
            "Array", "System.Array");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return context.Unhandled(node, "static Array");

        // Bound: the searches, the sorts and the finds as .NET answers them, each argument in its
        // parameter's place (#488, #425).
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method
            && Bound(methodName, invocation, method, context) is { } bound)
        {
            return Ir.JsExprWriter.Write(bound);
        }

        var arrayArg = context.Converter.ConvertExpression(args[0].Expression);

        switch (methodName)
        {
            case "Sort":
                // Array.Sort(array) -> array.sort()
                // Array.Sort(array, comparison) -> array.sort(comparison)
                if (args.Count == 1)
                    return $"{arrayArg}.sort()";
                else if (args.Count >= 2)
                {
                    var comparison = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.sort({comparison})";
                }
                break;

            case "Reverse":
                // Array.Reverse(array) -> array.reverse()
                return $"{arrayArg}.reverse()";

            case "Find":
                // Array.Find(array, predicate) -> array.find(predicate)
                if (args.Count >= 2)
                {
                    var predicate = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.find({predicate})";
                }
                break;

            case "FindIndex":
                // Array.FindIndex(array, predicate) -> array.findIndex(predicate)
                if (args.Count >= 2)
                {
                    var predicate = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.findIndex({predicate})";
                }
                break;

            case "FindAll":
                // Array.FindAll(array, predicate) -> array.filter(predicate)
                if (args.Count >= 2)
                {
                    var predicate = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.filter({predicate})";
                }
                break;

            case "IndexOf":
                // Array.IndexOf(array, value) -> array.indexOf(value)
                if (args.Count >= 2)
                {
                    var value = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.indexOf({value})";
                }
                break;

            case "LastIndexOf":
                // Array.LastIndexOf(array, value) -> array.lastIndexOf(value)
                if (args.Count >= 2)
                {
                    var value = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.lastIndexOf({value})";
                }
                break;

            case "Exists":
                // Array.Exists(array, predicate) -> array.some(predicate)
                if (args.Count >= 2)
                {
                    var predicate = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.some({predicate})";
                }
                break;

            case "TrueForAll":
                // Array.TrueForAll(array, predicate) -> array.every(predicate)
                if (args.Count >= 2)
                {
                    var predicate = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.every({predicate})";
                }
                break;

            case "Clear":
                // Array.Clear(array) -> array.splice(0)
                return $"{arrayArg}.splice(0)";

            case "Resize":
                // Array.Resize(ref array, newSize) -> array.length = newSize
                if (args.Count >= 2)
                {
                    var newSize = context.Converter.ConvertExpression(args[1].Expression);
                    return $"{arrayArg}.length = {newSize}";
                }
                break;
        }

        return context.Unhandled(node, "static Array");
    }

    /// <summary>
    /// <c>Sort</c> by .NET's introspective sort and the comparer it is handed; <c>IndexOf</c> and
    /// <c>LastIndexOf</c> by the element type's equality, a NaN, a record and a tuple found as
    /// <c>EqualityComparer&lt;T&gt;.Default</c> finds them, where <c>indexOf</c>'s <c>===</c> found none
    /// of them; <c>Find</c> and <c>FindLast</c> with the element type's default; and every range checked
    /// as .NET checks it (<c>utils/list.ts</c>). Null for the members whose array method answers alike.
    /// </summary>
    private static Ir.JsExpr? Bound(string name, InvocationExpressionSyntax invocation, IMethodSymbol method,
        ConversionContext context)
    {
        // The element type: T of the generic overload, or what the non-generic one compares, `object`.
        var element = method.TypeArguments.Length == 1
            ? method.TypeArguments[0]
            : name is "IndexOf" or "LastIndexOf" ? method.Parameters.ElementAtOrDefault(1)?.Type
            : method.Parameters.FirstOrDefault()?.Type is IArrayTypeSymbol array ? array.ElementType : null;
        var count = method.Parameters.Length;
        switch (name)
        {
            case "Sort":
            {
                context.UsedHelpers.Add(Eq.Import);
                if (method.Parameters.Any(parameter => parameter.Type.Name == "Array")
                    || method.TypeArguments.Length > 1)
                {
                    return Ir.JsExpr.Opaque(context.Unhandled(invocation, "Array.Sort of keys and items"));
                }
                if (method.Parameters is [_, { Type: INamedTypeSymbol { TypeKind: TypeKind.Delegate } comparison }])
                {
                    return ParameterTemplate.Call($"{Eq.ArraySortBy}({{0}}, {{1}}, '{ReflectionName.Of(comparison)}')",
                        null, invocation, method, context);
                }
                // (array), (array, comparer), (array, index, length), (array, index, length, comparer)
                int? comparer = count is 2 or 4 ? count - 1 : null;
                return SortOrders.Call(count >= 3
                        ? $"{Eq.ArraySort}({{0}}, {{order}}, {{1}}, {{2}})"
                        : $"{Eq.ArraySort}({{0}}, {{order}})",
                    null, invocation, method, comparer, element, context) ?? Ir.JsExpr.Literal("undefined");
            }
            case "IndexOf" or "LastIndexOf":
            {
                context.UsedHelpers.Add(Eq.Import);
                var helper = name == "IndexOf" ? Eq.ArrayIndexOf : Eq.ArrayLastIndexOf;
                var equality = ElementEquality.Of(element);
                var range = string.Concat(Enumerable.Range(2, count - 2).Select(slot => ", {" + slot + "}"));
                var equalityArgument = equality is null && count == 2 ? "" : $", {equality ?? "false"}";
                return ParameterTemplate.Call($"{helper}({{0}}, {{1}}{equalityArgument}{range})", null, invocation, method, context);
            }
            case "Find" or "FindLast":
            {
                context.UsedHelpers.Add(Eq.Import);
                var fallback = DefaultValue.Of(method.ReturnType, context);
                return ParameterTemplate.Call($"{(name == "Find" ? Eq.ArrayFind : Eq.ArrayFindLast)}({{0}}, {{1}}, {fallback})",
                    null, invocation, method, context);
            }
            case "FindIndex" or "FindLastIndex":
            {
                context.UsedHelpers.Add(Eq.Import);
                var helper = name == "FindIndex" ? Eq.ArrayFindIndex : Eq.ArrayFindLastIndex;
                // (array, match), (array, startIndex, match), (array, startIndex, count, match).
                return ParameterTemplate.Call(count switch
                {
                    2 => $"{helper}({{0}}, {{1}})",
                    3 => $"{helper}({{0}}, {{2}}, {{1}})",
                    _ => $"{helper}({{0}}, {{3}}, {{1}}, {{2}})",
                }, null, invocation, method, context);
            }
        }
        return null;
    }

    public int Priority => 20; // Higher than StringStaticStrategy to handle static array methods
}
