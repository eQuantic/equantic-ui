using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// What a method's body says about being an ITERATOR — asked from both emitters, so the answer is
/// defined once. A C# iterator is lazy; the emitted one MATERIALISES into an array, because every
/// sequence in the emitted world is an array and a JS generator would read as undefined the moment
/// a LINQ operator touched it. That trade is invisible for the sequences UI code actually builds,
/// and impossible for one that never ends — which is what <see cref="FindEndlessYieldLoop"/> is for.
/// </summary>
public static class IteratorShapeExtensions
{
    /// <summary>
    /// Whether this body yields in its OWN right. Lambdas and local functions carry their own
    /// iterator-ness, so a yield inside one says nothing about the method containing it.
    /// </summary>
    public static bool IsIteratorBody(this BlockSyntax? body) =>
        body.OwnStatements().OfType<YieldStatementSyntax>().Any();

    /// <summary>
    /// The loop in an iterator body that can never finish: it yields, and no path out of it exists
    /// — no <c>yield break</c>, no <c>return</c>, no <c>break</c> of its own, nothing thrown. In C#
    /// that is an ordinary infinite generator, useful because the caller stops taking. Materialised
    /// eagerly it is a hang, so it is worth a name rather than a frozen tab.
    /// </summary>
    public static SyntaxNode? FindEndlessYieldLoop(this BlockSyntax? body) =>
        body.OwnStatements()
            .FirstOrDefault(node => node.LoopBodyIfEndless() is { } loopBody
                                    && loopBody.OwnStatements().OfType<YieldStatementSyntax>()
                                        .Any(y => !y.IsKind(SyntaxKind.YieldBreakStatement))
                                    && !loopBody.HasWayOut());

    /// <summary>
    /// The body of a loop whose CONDITION never becomes false — <c>while (true)</c>, <c>for (;;)</c>
    /// and <c>do … while (true)</c>. Anything else is assumed to terminate; guessing further would
    /// be guessing.
    /// </summary>
    private static StatementSyntax? LoopBodyIfEndless(this SyntaxNode node) => node switch
    {
        WhileStatementSyntax w when w.Condition.IsAlwaysTrue() => w.Statement,
        DoStatementSyntax d when d.Condition.IsAlwaysTrue() => d.Statement,
        ForStatementSyntax f when f.Condition is null || f.Condition.IsAlwaysTrue() => f.Statement,
        _ => null,
    };

    private static bool IsAlwaysTrue(this ExpressionSyntax condition) =>
        condition is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.TrueLiteralExpression);

    /// <summary>Whether anything inside this loop body can leave it.</summary>
    private static bool HasWayOut(this StatementSyntax loopBody) =>
        loopBody.OwnStatements().Any(node => node.LeavesTheLoop(loopBody));

    /// <summary>
    /// Leaving the METHOD leaves the loop with it, wherever it sits. A <c>break</c> is the one that
    /// has to be read in context: it belongs to the nearest enclosing loop OR switch, so an inner
    /// loop's break — and a switch case's — is not this loop's way out.
    /// </summary>
    private static bool LeavesTheLoop(this SyntaxNode node, StatementSyntax loopBody) => node switch
    {
        BreakStatementSyntax => node.BindsTo(loopBody),
        ReturnStatementSyntax or ThrowStatementSyntax or GotoStatementSyntax => true,
        YieldStatementSyntax y => y.IsKind(SyntaxKind.YieldBreakStatement),
        _ => false,
    };

    /// <summary>Whether this <c>break</c> binds to the loop whose body is <paramref name="loopBody"/>.</summary>
    private static bool BindsTo(this SyntaxNode breakStatement, StatementSyntax loopBody)
    {
        foreach (var ancestor in breakStatement.Ancestors())
        {
            if (ancestor == loopBody.Parent) return true;   // reached OUR loop with nothing in between
            if (ancestor.IsBreakScope()) return false;      // something nearer already claimed it
        }
        return false;
    }

    private static bool IsBreakScope(this SyntaxNode node) =>
        node is WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax
            or ForEachStatementSyntax or SwitchStatementSyntax;

    /// <summary>Every node this body owns — the ones a nested lambda or local function owns are its own.</summary>
    private static IEnumerable<SyntaxNode> OwnStatements(this SyntaxNode? body) =>
        body?.DescendantNodesAndSelf(node => node is not AnonymousFunctionExpressionSyntax
                                             && node is not LocalFunctionStatementSyntax)
        ?? [];
}
