using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Distinct() to JavaScript [...new Set(array)]
/// </summary>
public class DistinctStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Distinct")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
        {
            return true;
        }

        // Fallback: If no semantic model or unresolved symbol, assume it's LINQ if the name matches
        if (context.SemanticModel == null || symbol == null)
        {
            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);

        // JS Set dedups by SameValueZero, which matches C# Distinct for primitives, strings,
        // enums AND plain reference types (reference equality). Only records and structs use
        // structural/value equality, where Set would keep equal-but-distinct instances — those
        // need a value-based dedup (keeping the first occurrence, like Distinct).
        var elementType = GetElementType(context.SemanticHelper.GetType(memberAccess.Expression));
        if (elementType is { } et && !IsPrimitiveOrString(et) && (et.IsRecord || et.TypeKind == TypeKind.Struct))
        {
            return $"(() => {{ const _seen = new Set(); return {caller}.filter(_x => {{ " +
                   $"const _k = JSON.stringify(_x); if (_seen.has(_k)) return false; _seen.add(_k); return true; }}); }})()";
        }

        // [...new Set(array)] creates a new array with unique values
        return $"[...new Set({caller})]";
    }

    private static ITypeSymbol? GetElementType(ITypeSymbol? collectionType)
    {
        if (collectionType is IArrayTypeSymbol array) return array.ElementType;
        if (collectionType is INamedTypeSymbol named)
        {
            if (named.TypeArguments.Length == 1) return named.TypeArguments[0];
            var enumerable = named.AllInterfaces
                .FirstOrDefault(i => i.Name == "IEnumerable" && i.TypeArguments.Length == 1);
            if (enumerable != null) return enumerable.TypeArguments[0];
        }
        return null;
    }

    private static bool IsPrimitiveOrString(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum) return true;
        return type.SpecialType is
            SpecialType.System_String or SpecialType.System_Boolean or SpecialType.System_Char or
            SpecialType.System_SByte or SpecialType.System_Byte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;
    }

    public int Priority => 10;
}
