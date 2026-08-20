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
public class DateTimeOffsetStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.DateTimeOffset";

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            // Target-typed `new(…)` included — see DateTimeStrategy for what missing it costs.
            case BaseObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc))
                    || (oc is ObjectCreationExpressionSyntax named && named.Type.ToString() == "DateTimeOffset");
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsMember(ma, context);
            case MemberAccessExpressionSyntax member:
                return IsMember(member, context);
            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return $"{Eq.DateTimeOffset}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.Text;
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                    return $"{Eq.DateTimeOffset}.{name.ToCamelCase()}({args})";
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{name.ToCamelCase()}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.Text;
                if (IsStaticAccess(member, context))
                    return $"{Eq.DateTimeOffset}.{name.ToCamelCase()}()"; // Now/UtcNow/MinValue/MaxValue -> factory methods
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{name.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "DateTimeOffset");
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

    public override int Priority => 15;
}
