using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts Enum Member access to string literals.
/// - Display.Flex -> 'flex'
/// </summary>
public class EnumStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var member = memberAccess.Name.Identifier.Text;
        
        // Exclude Nullable properties
        if (member == "Value" || member == "HasValue")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(node);
        if (symbol != null && symbol.Kind == SymbolKind.Field && symbol.ContainingType.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        // Heuristic fallback (same as original code)
        if (context.SemanticModel == null)
        {
            var expr = memberAccess.Expression.ToString();
            bool isPascalCase = !expr.Contains('.') &&
                               !expr.StartsWith("this.") &&
                               expr.Length > 0 &&
                               char.IsUpper(expr[0]) &&
                               char.IsUpper(member[0]);
            return isPascalCase;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var member = memberAccess.Name.Identifier.Text;

        return $"'{ToCamelCase(member)}'";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 5;
}
