using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for declaration expressions.
/// Handles:
/// - var (a, b) = ... converts to [a, b]
/// - out var x converts to x
/// </summary>
public class DeclarationExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is DeclarationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var decl = (DeclarationExpressionSyntax)node;
        
        if (decl.Designation is ParenthesizedVariableDesignationSyntax deconstruction)
        {
            var names = string.Join(", ", deconstruction.Variables
                .OfType<SingleVariableDesignationSyntax>()
                .Select(v => v.Identifier.Text));
            return $"[{names}]";
        }
        
        if (decl.Designation is SingleVariableDesignationSyntax single)
        {
            return single.Identifier.Text;
        }

        return decl.Designation.ToString();
    }

    public int Priority => 10;
}
