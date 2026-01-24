using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// General strategy for member access.
/// Handles property mapping (Length -> length) and standard naming conventions.
/// Serves as a fallback after specialized strategies (Enum, Nullable).
/// </summary>
public class MemberAccessStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is MemberAccessExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var name = memberAccess.Name.Identifier.Text;
        var expr = context.Converter.ConvertExpression(memberAccess.Expression);

        // Convert C# properties to JS
        // Note: Specialized mappings (HasValue, Value) are handled by NullableStrategy
        name = name switch
        {
            "Length" => "length",
            "Count" => "length", // Arrays/Lists
            _ => ToCamelCase(name)
        };

        if (string.IsNullOrEmpty(name)) return expr;

        return $"{expr}.{name}";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 0; // Low priority (fallback)
}
