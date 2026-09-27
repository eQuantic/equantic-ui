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
        // Array destructuring (tuples), and the name of an `out var`: the binding the scanner
        // declares, spelled as it spells it. A discard keeps its slot as a hole, so the remaining
        // names still line up positionally: `var (_, y) = (5, 7)` -> `[, y]`.
        return ExpressionVariableScanner.BindingPattern(decl.Designation);
    }

    public int Priority => 10;
}
