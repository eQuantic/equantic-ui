using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts <c>Nullable&lt;T&gt;</c> (<c>T?</c>) members/methods to JavaScript:
/// <list type="bullet">
/// <item><c>x.HasValue</c> → <c>(x != null)</c></item>
/// <item><c>x.Value</c> → <c>x</c></item>
/// <item><c>x.GetValueOrDefault()</c> → <c>(x ?? default(T))</c> — the default is derived from the
/// underlying type (0 / false / <c>$eq.num.dec(0)</c> / the enum's zero-member, …)</item>
/// <item><c>x.GetValueOrDefault(fallback)</c> → <c>(x ?? fallback)</c></item>
/// </list>
/// Priority 25, like <see cref="DictionaryStrategy"/>, which also answers a <c>GetValueOrDefault</c>;
/// the semantic gates (a <c>Nullable&lt;T&gt;</c> receiver here, a dictionary there) keep the two
/// from colliding. Lifted operators (<c>+ - * / &lt; &gt; …</c>) are handled by
/// BinaryExpressionStrategy.
/// </summary>
public class NullableStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.Text;
                if (name != "HasValue" && name != "Value") return false;
                return IsNullableReceiver(member.Expression, context, allowShapeHeuristic: true);
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
            {
                if (ma.Name.Identifier.Text != "GetValueOrDefault") return false;
                // Precise gate only (no shape heuristic): otherwise we'd steal Dictionary.GetValueOrDefault.
                return IsNullableReceiver(ma.Expression, context, allowShapeHeuristic: false);
            }

            default:
                return false;
        }
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case MemberAccessExpressionSyntax member:
            {
                var expr = context.Converter.ConvertExpression(member.Expression);
                return member.Name.Identifier.Text switch
                {
                    "HasValue" => $"({expr} != null)",
                    "Value" => expr,
                    _ => throw new InvalidOperationException(),
                };
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var expr = context.Converter.ConvertExpression(ma.Expression);
                var args = inv.ArgumentList.Arguments;
                if (args.Count >= 1)
                {
                    // GetValueOrDefault(fallback) -> (x ?? fallback)
                    var fallback = context.Converter.ConvertExpression(args[0].Expression);
                    return $"({expr} ?? {fallback})";
                }

                // GetValueOrDefault() -> (x ?? default(T))
                var underlying = UnderlyingType(context.SemanticHelper.GetType(ma.Expression));
                var def = DefaultFor(underlying, context);
                return $"({expr} ?? {def})";
            }

            default:
                return context.Unhandled(node, "Nullable");
        }
    }

    private static bool IsNullableReceiver(ExpressionSyntax receiver, ConversionContext context, bool allowShapeHeuristic)
    {
        var type = context.SemanticHelper.GetType(receiver);
        if (type != null && type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return true;

        // Shape fallback ONLY where guessing is honest (see ConversionContext.CanGuess) AND the
        // receiver's type did not resolve to something else — a RESOLVED non-Nullable receiver is
        // semantic evidence against, never a shape to guess at. Deliberately NOT used for
        // GetValueOrDefault, so Dictionary.GetValueOrDefault keeps working without a model.
        return allowShapeHeuristic && type == null && context.CanGuess(receiver);
    }

    private static ITypeSymbol? UnderlyingType(ITypeSymbol? nullableType)
    {
        if (nullableType is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }
        return nullableType;
    }

    /// <summary>JS for <c>default(T)</c> of the nullable's underlying value type.</summary>
    private static string DefaultFor(ITypeSymbol? t, ConversionContext context)
    {
        switch (t?.SpecialType)
        {
            case SpecialType.System_Boolean:
                return "false";
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
                return "0";
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                context.UsedHelpers.Add(Eq.Import);
                return $"{Eq.Long}(0)"; // BigInt 0n (JSON-serialized as a string by the runtime)
            case SpecialType.System_Decimal:
                context.UsedHelpers.Add(Eq.Import);
                return $"{Eq.Dec}(0)";
        }

        // An enum's default is its value 0 as the browser holds it, by the one rule
        // (EnumShape.HeldLiteral): the zero member's camelCase key, a flags enum's 0, a 64-bit one's
        // 0n. It was the member's DECLARED name in double quotes, which no enum value is here.
        if (t is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return EnumShape.HeldLiteral(enumType, 0);

        // char / DateTime / Guid / other structs: no faithful no-arg default modelled — use the
        // explicit GetValueOrDefault(fallback) form for those.
        return "null";
    }

    public int Priority => 25;
}
