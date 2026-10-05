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

    /// <summary>
    /// Whether <paramref name="node"/> sits in a checked context, read off the syntax and the
    /// compilation's own setting rather than the bound tree: the nearest enclosing checked or
    /// unchecked construct decides, and none means the project's. For an ENUM's conversion, which the
    /// bound tree reports unchecked even inside <c>checked(…)</c> (IsChecked is false for an
    /// ExplicitEnumeration, measured on Roslyn 5.9), where the runtime throws:
    /// <c>checked((Tiny)300)</c> and <c>checked((int)aLongEnum)</c> are OverflowExceptions in .NET.
    /// </summary>
    internal static bool IsCheckedAt(SyntaxNode node, ConversionContext context)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case CheckedExpressionSyntax expression:
                    return expression.IsKind(SyntaxKind.CheckedExpression);
                case CheckedStatementSyntax statement:
                    return statement.IsKind(SyntaxKind.CheckedStatement);
                case MemberDeclarationSyntax:
                    return context.SemanticModel?.Compilation.Options.CheckOverflow == true;
            }
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
