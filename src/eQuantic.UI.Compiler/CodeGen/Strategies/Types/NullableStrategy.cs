using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Converts Nullable properties to JavaScript.
/// - prop.HasValue -> prop != null
/// - prop.Value -> prop
/// </summary>
public class NullableStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var member = memberAccess.Name.Identifier.Text;
        if (member != "HasValue" && member != "Value")
            return false;

        // Semantic Check: Is it actually a Nullable<T>?
        var type = context.SemanticHelper.GetType(memberAccess.Expression);
        if (type != null && type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        // Fallback: heuristic based on name suffix (legacy)
        if (context.SemanticModel == null)
        {
            // This is risky but matches existing logic for legacy mode
            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var member = memberAccess.Name.Identifier.Text;
        var expr = context.Converter.ConvertExpression(memberAccess.Expression);

        return member switch
        {
            "HasValue" => $"({expr} != null)",
            "Value" => expr,
            _ => throw new InvalidOperationException()
        };
    }

    public int Priority => 15;
}
