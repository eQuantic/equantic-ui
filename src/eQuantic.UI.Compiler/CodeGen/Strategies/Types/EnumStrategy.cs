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

        // Semantic check. When the semantic model resolved a symbol, TRUST it: it's an enum member only
        // if the symbol is an enum field. Anything else with the same PascalCase.Upper shape — a property
        // (e.g. List.Count), a static field (Widget.Items), a method — is NOT an enum, so we must not fall
        // through to the loose heuristic below (which would mistranslate Items.Count → 'count').
        var symbol = context.SemanticHelper.GetSymbol(node);
        if (symbol != null)
        {
            return symbol.Kind == SymbolKind.Field && symbol.ContainingType?.TypeKind == TypeKind.Enum;
        }

        // Nullable's two members, known by their names where the model cannot say. Read before the
        // model was asked, they made an enum member called Value or HasValue a plain member access:
        // CodeCompletionKind.Value reached the browser as a property of an object nothing defines.
        if (member == "Value" || member == "HasValue")
            return false;

        // Heuristic fallback — ONLY where guessing is honest (see ConversionContext.CanGuess).
        // Under an authoritative model, an in-tree PascalCase access that did not bind must NOT
        // become an enum-member string by shape: it is missing references or non-compiling code,
        // and the member-access fallback reports it instead.
        if (!context.CanGuess(node)) return false;
        var expr = memberAccess.Expression.ToString();
        if (context.IsFallbackTypeReceiver(expr)) return false;

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
        return context.SemanticHelper.GetSymbol(node) is IFieldSymbol field
            ? MemberLiteral(field)
            : $"'{memberAccess.Name.Identifier.Text.ToCamelCase()}'";
    }

    /// <summary>
    /// An enum member as the browser holds it, by its symbol, whichever way the source reached it: a
    /// qualified <c>Level.High</c> and a bare <c>High</c> under <c>using static</c> alike (#485).
    /// <para>
    /// A [Flags] enum is represented NUMERICALLY: its members exist to be OR-combined (`Read | Write`),
    /// which a member-name string cannot express, so the underlying value keeps bitwise ops, HasFlag
    /// and casts behaving like .NET. Every other enum is its member name as a string (SizeVariant.Medium
    /// is 'medium'), never its number: verbose, easy to identify, and stable whatever the underlying
    /// value. camelCase matches the runtime's theme lookups. A flags value is read through decimal,
    /// which holds every underlying type's range: a ulong member past long's crashed the compile.
    /// </para>
    /// </summary>
    internal static string MemberLiteral(IFieldSymbol field) =>
        field.ContainingType.IsFlagsEnum() && field.HasConstantValue
            ? System.Convert.ToDecimal(field.ConstantValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            : $"'{field.Name.ToCamelCase()}'";

    public int Priority => 5;
}
