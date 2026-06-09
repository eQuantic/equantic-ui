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
/// Priority 25 — above <see cref="Invocation.DictionaryStrategy"/> (20), which also matches the
/// <c>GetValueOrDefault</c> name; the semantic gate here (receiver is <c>Nullable&lt;T&gt;</c>) keeps
/// the two from colliding. Lifted operators (<c>+ - * / &lt; &gt; …</c>) are handled by
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
                return IsNullableReceiver(member.Expression, context, allowLegacyHeuristic: true);
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
            {
                if (ma.Name.Identifier.Text != "GetValueOrDefault") return false;
                // Precise gate only (no legacy heuristic): otherwise we'd steal Dictionary.GetValueOrDefault.
                return IsNullableReceiver(ma.Expression, context, allowLegacyHeuristic: false);
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
                return node.ToString();
        }
    }

    private static bool IsNullableReceiver(ExpressionSyntax receiver, ConversionContext context, bool allowLegacyHeuristic)
    {
        var type = context.SemanticHelper.GetType(receiver);
        if (type != null && type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return true;

        // Legacy fallback (no semantic model): trust the .HasValue/.Value shape. Deliberately NOT used
        // for GetValueOrDefault, so Dictionary.GetValueOrDefault keeps working without a semantic model.
        return allowLegacyHeuristic && context.SemanticModel == null;
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

        // Enum: default is the member whose constant value is 0 (its name; enums are member-name
        // strings at runtime). If no zero-valued member exists, .NET still yields the numeric 0.
        if (t is { TypeKind: TypeKind.Enum })
        {
            var zero = t.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.HasConstantValue && IsZero(f.ConstantValue));
            return zero != null ? $"\"{zero.Name}\"" : "0";
        }

        // char / DateTime / Guid / other structs: no faithful no-arg default modelled — use the
        // explicit GetValueOrDefault(fallback) form for those.
        return "null";
    }

    private static bool IsZero(object? constant)
    {
        try { return constant != null && System.Convert.ToInt64(constant) == 0; }
        catch { return false; }
    }

    public int Priority => 25;
}
