using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Special;

/// <summary>
/// Strategy for default keyword/expression.
/// Handles:
/// - default(int) -> 0
/// - default(string) -> null
/// - default(bool) -> false
/// - default(T) -> undefined (for reference types)
/// - default -> undefined (contextual)
/// </summary>
public class DefaultKeywordStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Handle default(T) expression
        if (node is DefaultExpressionSyntax)
            return true;

        // Handle default literal
        if (node is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.DefaultLiteralExpression)
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // default(T) expression
        if (node is DefaultExpressionSyntax defaultExpr)
        {
            var typeName = defaultExpr.Type.ToString();
            return MapDefaultValue(typeName);
        }

        // default literal (contextual)
        return "undefined";
    }

    private string MapDefaultValue(string typeName)
    {
        // Remove nullable syntax
        if (typeName.EndsWith("?"))
            typeName = typeName[..^1];

        return typeName switch
        {
            // Numeric types -> 0
            "int" or "Int32" or "System.Int32" => "0",
            "long" or "Int64" or "System.Int64" => "0",
            "short" or "Int16" or "System.Int16" => "0",
            "byte" or "Byte" or "System.Byte" => "0",
            "sbyte" or "SByte" or "System.SByte" => "0",
            "uint" or "UInt32" or "System.UInt32" => "0",
            "ulong" or "UInt64" or "System.UInt64" => "0",
            "ushort" or "UInt16" or "System.UInt16" => "0",

            // Floating point -> 0.0
            "float" or "Single" or "System.Single" => "0.0",
            "double" or "Double" or "System.Double" => "0.0",
            "decimal" or "Decimal" or "System.Decimal" => "0.0",

            // Boolean -> false
            "bool" or "Boolean" or "System.Boolean" => "false",

            // Char -> '\0' (empty string in JS)
            "char" or "Char" or "System.Char" => "''",

            // Reference types and value types not explicitly handled -> null/undefined
            _ when IsValueType(typeName) => "undefined", // struct default
            _ => "null" // reference type default
        };
    }

    private bool IsValueType(string typeName)
    {
        // Check if it's likely a struct (uppercase first letter, not in common reference type list)
        if (string.IsNullOrEmpty(typeName))
            return false;

        var commonReferenceTypes = new[]
        {
            "string", "String", "System.String",
            "object", "Object", "System.Object",
            "Array", "List", "Dictionary", "IEnumerable"
        };

        return !commonReferenceTypes.Any(typeName.Contains);
    }

    public int Priority => 15; // Higher than LiteralExpressionStrategy
}
