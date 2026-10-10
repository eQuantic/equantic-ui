using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.DateTime</c> to the runtime <c>DateTime</c> compat type (tick-precise, .NET-faithful).
/// Statics route through the <c>dateTime</c> factory; instance members/methods become camelCase calls on
/// the value. A constructor is <see cref="DateTimeConstructionStrategy"/>'s, and operators (+ - and
/// comparisons) are handled by BinaryExpressionStrategy.
/// </summary>
/// <remarks>
/// Priority 15 so it wins over the generic ToString (10), ObjectCreation (5) and member-access (0)
/// strategies for DateTime nodes. Gated on the semantic type, so only genuine DateTime nodes are taken.
/// </remarks>
public class DateTimeStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.DateTime";

    /// <summary>The methods that are a standard specifier by another name.</summary>
    private static readonly Dictionary<string, string> CultureStrings = new(StringComparer.Ordinal)
    {
        ["ToShortDateString"] = "d",
        ["ToLongDateString"] = "D",
        ["ToShortTimeString"] = "t",
        ["ToLongTimeString"] = "T",
    };

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            // A value's ToString is the formatter's (ToStringStrategy), as a number's is: the twin's
            // toString(pattern) knew custom tokens only, so `d.ToString("D")` printed `D`, and a
            // provider crossed to the browser as a name no browser has (#388).
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "ToString" } }:
                return false;

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsDateTimeMember(ma, context);

            case MemberAccessExpressionSyntax member:
                return IsDateTimeMember(member, context);

            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.Text;
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                {
                    return $"{Eq.DateTime}.{name.ToCamelCase()}({args})";
                }
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                // The short and long date and time strings are .NET's ToString("d"), ("D"), ("t")
                // and ("T") in the current culture, and go where ToString goes; the twin has no
                // method by those names, so each was a TypeError in the browser (found in review, #388).
                if (CultureStrings.TryGetValue(name, out var specifier))
                    return $"{Eq.Format}({receiver}, '{specifier}')";
                return $"{receiver}.{name.ToCamelCase()}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.Text;
                if (IsStaticAccess(member, context))
                {
                    // Static properties (Now/UtcNow/Today/MinValue/MaxValue) map to factory methods.
                    return $"{Eq.DateTime}.{name.ToCamelCase()}()";
                }
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{name.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "DateTime");
        }
    }

    private static bool IsDateTimeMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        // No semantic info: static access via the type name, or an instance whose type we can read.
        if (ma.Expression.ToString() == "DateTime") return true;
        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return ma.Expression.ToString() == "DateTime";
    }

    private static bool IsType(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }
        return type?.ToDisplayString() == TypeName;
    }

    public override int Priority => 15;
}
