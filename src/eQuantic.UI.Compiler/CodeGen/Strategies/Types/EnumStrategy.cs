using System.Linq;
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

        // Heuristic fallback (if semantic fails or is missing)
        var expr = memberAccess.Expression.ToString();
        bool isPascalCase = !expr.Contains('.') &&
                           !expr.StartsWith("this.") &&
                           expr.Length > 0 &&
                           char.IsUpper(expr[0]) &&
                           char.IsUpper(member[0]);
        return isPascalCase;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var member = memberAccess.Name.Identifier.Text;

        // Try to get the actual enum value from semantic model
        var symbol = context.SemanticHelper.GetSymbol(node);

        // Check if this is an enum field
        if (symbol is IFieldSymbol fieldSymbol &&
            fieldSymbol.ContainingType?.TypeKind == TypeKind.Enum)
        {
            // Try to get constant value
            if (fieldSymbol.HasConstantValue)
            {
                return fieldSymbol.ConstantValue?.ToString() ?? "0";
            }

            // Fallback: calculate enum value by position (most enums start at 0 and increment)
            var enumType = fieldSymbol.ContainingType;
            var members = enumType.GetMembers().OfType<IFieldSymbol>()
                .Where(f => f.IsConst && f.HasConstantValue)
                .ToList();

            var index = members.FindIndex(m => m.Name == member);
            if (index >= 0)
            {
                // Try to get the actual constant value
                var enumMember = members[index];
                if (enumMember.ConstantValue != null)
                {
                    return enumMember.ConstantValue.ToString() ?? index.ToString();
                }
                return index.ToString();
            }
        }

        // Fallback to string (for enums used as string values like CSS classes)
        return $"'{ToCamelCase(member)}'";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 5;
}
