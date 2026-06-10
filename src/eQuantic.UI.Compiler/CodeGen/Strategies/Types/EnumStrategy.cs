using System.Globalization;
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

        // Semantic check. When the semantic model resolved a symbol, TRUST it: it's an enum member only
        // if the symbol is an enum field. Anything else with the same PascalCase.Upper shape — a property
        // (e.g. List.Count), a static field (Widget.Items), a method — is NOT an enum, so we must not fall
        // through to the loose heuristic below (which would mistranslate Items.Count → 'count').
        var symbol = context.SemanticHelper.GetSymbol(node);
        if (symbol != null)
        {
            return symbol.Kind == SymbolKind.Field && symbol.ContainingType?.TypeKind == TypeKind.Enum;
        }

        // Heuristic fallback — ONLY when there is no semantic info to rely on.
        var expr = memberAccess.Expression.ToString();
        bool isPascalCase = !expr.Contains('.') &&
                           !expr.StartsWith("this.") &&
                           expr.Length > 0 &&
                           char.IsUpper(expr[0]) &&
                           char.IsUpper(member[0]);
                           
        if (!isPascalCase) return false;

        // Enums cannot be assigned to
        if (node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node)
            return false;

        return true;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var member = memberAccess.Name.Identifier.Text;

        // A [Flags] enum is represented NUMERICALLY: its members exist to be OR-combined (`Read | Write`),
        // which a member-name string cannot express. Emit the underlying value so bitwise ops / HasFlag /
        // casts behave like .NET.
        if (context.SemanticHelper.GetSymbol(node) is IFieldSymbol field
            && field.ContainingType.IsFlagsEnum()
            && field.HasConstantValue)
        {
            return System.Convert.ToInt64(field.ConstantValue, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture);
        }

        // Non-flags enums are represented by their member name as a string (e.g. SizeVariant.Medium ->
        // 'medium'), never by their numeric value. A string is verbose and easy to identify,
        // and is stable regardless of the underlying value or build configuration (previously
        // the semantic path emitted a number while the fallback emitted a string). camelCase
        // matches the runtime convention used by theme lookups (GetSize/GetVariant) and the
        // case-insensitive parseEnum / Enum.* helpers.
        return $"'{member.ToCamelCase()}'";
    }

    public int Priority => 5;
}
