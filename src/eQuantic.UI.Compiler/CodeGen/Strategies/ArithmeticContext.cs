using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Whether an arithmetic sits in a <c>checked</c> context — the bound tree's answer
/// (<see cref="IBinaryOperation.IsChecked"/> and its siblings), which folds the project-wide
/// setting, <c>checked</c> blocks and <c>checked(…)</c> expressions into one bit the syntax never
/// shows — and whether the author wrote an explicit <c>unchecked</c>, which is what makes an
/// <c>int</c> wrap on this side (see <see cref="IntegerWidth"/>).
/// </summary>
public readonly record struct ArithmeticContext(bool IsChecked, bool ExplicitUnchecked)
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
        return new ArithmeticContext(isChecked, ExplicitlyUnchecked(node));
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
