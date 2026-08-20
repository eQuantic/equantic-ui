using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.DateOnly</c> and <c>System.TimeOnly</c> to the runtime compat types:
/// <c>new DateOnly/TimeOnly(...)</c> -> <c>$eq.time.dateOnly/timeOnly(...)</c>; statics
/// (<c>MinValue</c>, <c>FromDateTime</c>, <c>Parse</c>, …) -> factory members; instance
/// members/methods -> camelCase. Priority 15, gated on the receiver type (robust against members
/// that resolve to interfaces/extensions).
/// </summary>
public class DateOnlyTimeOnlyStrategy : ConversionStrategyBase
{
    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return KindOf(context.SemanticHelper.GetType(oc)) != null
                    || (oc is ObjectCreationExpressionSyntax named && KindOfName(named.Type.ToString()) != null);

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return Kind(ma, context) != null;

            case MemberAccessExpressionSyntax member:
                return Kind(member, context) != null;

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
            {
                var kind = KindOf(context.SemanticHelper.GetType(oc))
                    ?? (oc is ObjectCreationExpressionSyntax named ? KindOfName(named.Type.ToString()) : null);
                return $"$eq.time.{kind}({ConvertArgs(oc.ArgumentList, context)})";
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.Text;
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                    return $"$eq.time.{StaticKind(ma, context)}.{name.ToCamelCase()}({args})";
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{name.ToCamelCase()}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.Text;
                if (IsStaticAccess(member, context))
                    // Static properties (MinValue/MaxValue) map to factory methods.
                    return $"$eq.time.{StaticKind(member, context)}.{name.ToCamelCase()}()";
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{name.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "DateOnly/TimeOnly");
        }
    }

    /// <summary>Kind ("dateOnly"/"timeOnly") for a member access, by receiver type then member symbol.</summary>
    private static string? Kind(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var byReceiver = KindOf(context.SemanticHelper.GetType(ma.Expression));
        if (byReceiver != null) return byReceiver;
        var symbol = context.SemanticHelper.GetSymbol(ma);
        var bySymbol = symbol?.ContainingType != null ? KindOf(symbol.ContainingType) : null;
        if (bySymbol != null) return bySymbol;
        return KindOfName(ma.Expression.ToString()); // static via type name (no semantic model)
    }

    private static string StaticKind(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        var bySymbol = symbol?.ContainingType != null ? KindOf(symbol.ContainingType) : null;
        return bySymbol ?? KindOfName(ma.Expression.ToString()) ?? "dateOnly";
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return KindOfName(ma.Expression.ToString()) != null; // "DateOnly"/"TimeOnly"
    }

    private static string? KindOf(ITypeSymbol? type) => type?.ToDisplayString() switch
    {
        "System.DateOnly" => "dateOnly",
        "System.TimeOnly" => "timeOnly",
        _ => null,
    };

    private static string? KindOfName(string name) =>
        name == "DateOnly" ? "dateOnly" : name == "TimeOnly" ? "timeOnly" : null;

    public override int Priority => 15;
}
