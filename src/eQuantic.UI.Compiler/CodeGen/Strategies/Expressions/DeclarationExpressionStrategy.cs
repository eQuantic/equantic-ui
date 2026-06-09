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
            // Array destructuring (tuples). Discards (`_`) keep their slot as a hole so the remaining
            // names still line up positionally: `var (_, y) = (5, 7)` -> `[, y]`.
            var names = deconstruction.Variables.Select(v =>
                v is SingleVariableDesignationSyntax s && s.Identifier.Text != "_" ? s.Identifier.Text : "");
            return $"[{string.Join(", ", names)}]";
        }
        
        if (decl.Designation is SingleVariableDesignationSyntax single)
        {
            return single.Identifier.Text;
        }

        return decl.Designation.ToString();
    }

    public int Priority => 10;
}
