using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.Text.StringBuilder</c> to the runtime <c>StringBuilder</c> compat type.
/// <c>new StringBuilder(...)</c> becomes the <c>stringBuilder(...)</c> factory; instance methods
/// (<c>Append</c>, <c>AppendLine</c>, <c>Insert</c>, <c>Remove</c>, <c>Replace</c>, <c>Clear</c>,
/// <c>ToString</c>) and <c>Length</c> become their camelCase equivalents on the value.
/// </summary>
/// <remarks>
/// Priority 15 so it wins over the generic ToString (10), ObjectCreation (5) and member-access (0)
/// strategies for StringBuilder nodes. Gated on the semantic type (falls back to the type name when no
/// semantic model is present).
/// </remarks>
public class StringBuilderStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.Text.StringBuilder";

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc))
                    || (oc is ObjectCreationExpressionSyntax named && named.Type.ToString() == "StringBuilder");

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
                return $"{Eq.StringBuilder}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                var name = ma.Name.Identifier.Text;
                return $"{receiver}.{name.ToCamelCase()}({ConvertArgs(inv.ArgumentList, context)})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{member.Name.Identifier.Text.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "StringBuilder");
        }
    }

    private static bool IsMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    private static bool IsType(ITypeSymbol? type) => type?.ToDisplayString() == TypeName;

    public override int Priority => 15;
}
