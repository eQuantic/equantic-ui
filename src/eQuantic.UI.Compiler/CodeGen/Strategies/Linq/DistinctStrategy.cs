using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Distinct() to JavaScript [...new Set(array)]
/// </summary>
public class DistinctStrategy : IExpressionIrStrategy
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

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
        {
            return true;
        }

        return false;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        // A comparer is the collection fence's to judge (#578): one that asks for the element type's own
        // equality, which the shapes below already are, is dropped, and any other is refused, the call
        // written as its own C# text, as ToHashSet's is. It was dropped whatever it asked for, so
        // `Distinct(StringComparer.OrdinalIgnoreCase)` kept "a" and "A" in the browser alone.
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (context.SemanticHelper.GetOperation(argument.Expression) is not { } comparer)
                return JsExpr.Opaque(context.Unhandled(invocation, "Distinct with a comparer"));
            if (comparer.RefusesAsUntranslatable("Distinct", context)) return JsExpr.Opaque(invocation.ToString());
        }

        var source = LinqSource.Ir(memberAccess.Expression, context);

        // JS Set dedups by SameValueZero, which matches C# Distinct for primitives, strings,
        // enums AND plain reference types (reference equality). Only records and structs use
        // structural/value equality, where Set would keep equal-but-distinct instances — those
        // need a value-based dedup (keeping the first occurrence, like Distinct). The filter's
        // names take a `$`, which no C# name holds (#397), and the set it remembers is made once.
        var elementType = context.SemanticHelper.GetType(memberAccess.Expression).GetEnumerableElementType();
        if (elementType is { } et && !IsPrimitiveOrString(et) && (et.IsRecord || et.TypeKind == TypeKind.Struct))
        {
            return JsExpr.Template("{0}.filter((($seen) => ($x) => { const $k = JSON.stringify($x); " +
                                   "if ($seen.has($k)) return false; $seen.add($k); return true; })(new Set()))", [source]);
        }

        // [...new Set(array)] creates a new array with unique values
        return JsExpr.Template("[...new Set({0})]", [source]);
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
