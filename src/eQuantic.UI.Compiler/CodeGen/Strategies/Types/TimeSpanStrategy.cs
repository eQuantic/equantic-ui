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
public class TimeSpanStrategy : IConversionStrategy
{
    private const string TypeName = "System.TimeSpan";

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc)) || oc.Type.ToString() == "TimeSpan";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsTimeSpanMember(ma, context);

            case MemberAccessExpressionSyntax member:
                return IsTimeSpanMember(member, context);

            default:
                return false;
        }
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case ObjectCreationExpressionSyntax oc:
                return $"{Eq.TimeSpan}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.Text;
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                {
                    return $"{Eq.TimeSpan}.{Camel(name)}({args})";
                }
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{Camel(name)}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.Text;
                if (IsStaticAccess(member, context))
                {
                    // Static properties (Zero/MinValue/MaxValue). Zero is a field, the others methods.
                    return name == "Zero" ? $"{Eq.TimeSpan}.zero" : $"{Eq.TimeSpan}.{Camel(name)}()";
                }
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{Camel(name)}";
            }

            default:
                return node.ToString();
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

    private static string ConvertArgs(ArgumentListSyntax? argumentList, ConversionContext context)
    {
        if (argumentList == null || argumentList.Arguments.Count == 0) return string.Empty;
        return string.Join(", ", argumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)));
    }

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    public int Priority => 15;
}
