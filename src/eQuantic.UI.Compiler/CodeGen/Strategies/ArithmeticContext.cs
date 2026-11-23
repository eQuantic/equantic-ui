using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// How an arithmetic on a fixed-width integer settles (<see cref="IntegerWidth"/>), from the bound tree:
/// whether it sits in a <c>checked</c> context (<see cref="IBinaryOperation.IsChecked"/> and its
/// siblings, which fold the project-wide setting, <c>checked</c> blocks and <c>checked(…)</c>
/// expressions into one bit the syntax never shows), and whether C#'s compiler folded it into a constant
/// it proved in range. Outside an explicit <c>unchecked</c>, a constant that overflows is a compile error
/// (CS0220), so its value needs no settling and <c>-5</c> stays <c>-5</c>; inside one, the compiler
/// wrapped it, and the browser wraps it too.
/// </summary>
public readonly record struct ArithmeticContext(bool IsChecked, bool ProvedInRange)
{
    public static ArithmeticContext Of(SyntaxNode node, ConversionContext context)
    {
        var isChecked = context.SemanticHelper.GetOperation(node) switch
        {
            IBinaryOperation binary => binary.IsChecked,
            IUnaryOperation unary => unary.IsChecked,
            IIncrementOrDecrementOperation step => step.IsChecked,
            ICompoundAssignmentOperation compound => compound.IsChecked,
            _ => false,
        };
        var proved = context.SemanticHelper.TryGetConstantValue(node, out _) && !ExplicitlyUnchecked(node);
        return new ArithmeticContext(isChecked, proved);
    }

    /// <summary>
    /// Whether a conversion the bound tree holds no such bit for sits in a checked context: an enum's and a
    /// tuple's, whose <see cref="IConversionOperation.IsChecked"/> is false in <c>checked((Tone)d)</c> too, measured. The
    /// nearest enclosing construct decides, and the compilation's own setting where there is none.
    /// </summary>
    internal static bool ChecksOverflow(SyntaxNode node, ConversionContext context)
    {
        for (var current = node.Parent; current is not null and not MemberDeclarationSyntax; current = current.Parent)
        {
            if (current is CheckedExpressionSyntax expression) return expression.IsKind(SyntaxKind.CheckedExpression);
            if (current is CheckedStatementSyntax statement) return statement.IsKind(SyntaxKind.CheckedStatement);
        }
        return context.SemanticModel?.Compilation.Options.CheckOverflow == true;
    }

    /// <summary>The nearest enclosing checked/unchecked construct decides; none means the default.</summary>
    private static bool ExplicitlyUnchecked(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case CheckedExpressionSyntax expression:
                    return expression.IsKind(SyntaxKind.UncheckedExpression);
                case CheckedStatementSyntax statement:
                    return statement.IsKind(SyntaxKind.UncheckedStatement);
                case MemberDeclarationSyntax:
                    return false;
            }
        }
        return false;
    }
}
