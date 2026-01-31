using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for Array static methods.
/// Handles:
/// - Array.Sort(array) -> array.sort()
/// - Array.Reverse(array) -> array.reverse()
/// - Array.Find(array, predicate) -> array.find(predicate)
/// - Array.FindIndex(array, predicate) -> array.findIndex(predicate)
/// - Array.FindAll(array, predicate) -> array.filter(predicate)
/// - Array.IndexOf(array, value) -> array.indexOf(value)
/// - Array.LastIndexOf(array, value) -> array.lastIndexOf(value)
/// - Array.Exists(array, predicate) -> array.some(predicate)
/// - Array.TrueForAll(array, predicate) -> array.every(predicate)
/// </summary>
public class ArrayStaticStrategy : IConversionStrategy
{
    private static readonly HashSet<string> SupportedMethods = new()
    {
        "Sort", "Reverse", "Find", "FindIndex", "FindAll",
        "IndexOf", "LastIndexOf", "Exists", "TrueForAll", "Clear", "Resize"
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var typeExpression = memberAccess.Expression.ToString();
        var methodName = memberAccess.Name.Identifier.Text;

        // Check for Array.Method or System.Array.Method
        if (typeExpression != "Array" && typeExpression != "System.Array")
            return false;

        return SupportedMethods.Contains(methodName);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 0) return node.ToString();

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

        return node.ToString();
    }

    public int Priority => 20; // Higher than StringStaticStrategy to handle static array methods
}
