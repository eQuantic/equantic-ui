using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.DateTimeOffset</c> to the runtime <c>dateTimeOffset</c> compat type.
/// <c>new DateTimeOffset(...)</c> -> <c>$eq.time.dateTimeOffset(...)</c>; statics (<c>Now</c>,
/// <c>FromUnixTimeSeconds</c>, <c>Parse</c>, …) -> factory members; instance members/methods ->
/// camelCase. Operators (+ - and comparisons) are handled by BinaryExpressionStrategy. Priority 15,
/// gated on the receiver type (robust against members resolving to interfaces).
/// </summary>
public class DateTimeOffsetStrategy : IConversionStrategy
{
    private const string TypeName = "System.DateTimeOffset";

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc)) || oc.Type.ToString() == "DateTimeOffset";
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsMember(ma, context);
            case MemberAccessExpressionSyntax member:
                return IsMember(member, context);
            default:
                return false;
        }
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return $"{Eq.DateTimeOffset}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.Text;
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                    return $"{Eq.DateTimeOffset}.{Camel(name)}({args})";
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{Camel(name)}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.Text;
                if (IsStaticAccess(member, context))
                    return $"{Eq.DateTimeOffset}.{Camel(name)}()"; // Now/UtcNow/MinValue/MaxValue -> factory methods
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{Camel(name)}";
            }

            default:
                return node.ToString();
        }
    }

    private static bool IsMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        if (IsType(context.SemanticHelper.GetType(ma.Expression))) return true;
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null && symbol.ContainingType.ToDisplayString() == TypeName) return true;
        return ma.Expression.ToString() == "DateTimeOffset";
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return ma.Expression.ToString() == "DateTimeOffset";
    }

    private static bool IsType(ITypeSymbol? type) => type?.ToDisplayString() == TypeName;

    private static string ConvertArgs(ArgumentListSyntax? argumentList, ConversionContext context)
    {
        if (argumentList == null || argumentList.Arguments.Count == 0) return string.Empty;
        return string.Join(", ", argumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)));
    }

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    public int Priority => 15;
}
