using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.TimeSpan</c> to the runtime <c>TimeSpan</c> compat type (tick-precise, .NET "c"
/// formatting). Construction and statics (<c>FromSeconds</c>, …) route through the <c>timeSpan</c>
/// factory; instance members/methods become camelCase calls. Operators are handled by
/// BinaryExpressionStrategy. Priority 15, gated on the semantic type.
/// </summary>
public class TimeSpanStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.TimeSpan";

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc))
                    || (oc is ObjectCreationExpressionSyntax named && named.Type.ToString() == "TimeSpan");

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsTimeSpanMember(ma, context);

            case MemberAccessExpressionSyntax member:
                return IsTimeSpanMember(member, context);

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
                return $"{Eq.TimeSpan}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.Text;
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                {
                    return $"{Eq.TimeSpan}.{name.ToCamelCase()}({args})";
                }
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{name.ToCamelCase()}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.Text;
                if (IsStaticAccess(member, context))
                {
                    // Static properties (Zero/MinValue/MaxValue). Zero is a field, the others methods.
                    return name == "Zero" ? $"{Eq.TimeSpan}.zero" : $"{Eq.TimeSpan}.{name.ToCamelCase()}()";
                }
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{name.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "TimeSpan");
        }
    }

    private static bool IsTimeSpanMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        if (ma.Expression.ToString() == "TimeSpan") return true;
        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return ma.Expression.ToString() == "TimeSpan";
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
