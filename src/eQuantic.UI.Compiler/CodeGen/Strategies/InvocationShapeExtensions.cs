using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A method call reaches a strategy in TWO shapes, and until this existed only one of them was ever
/// recognised: <c>list.Contains(x)</c> is a <see cref="MemberAccessExpressionSyntax"/> with the
/// receiver attached, while <c>list?.Contains(x)</c> puts the member in a
/// <see cref="MemberBindingExpressionSyntax"/> and hangs the receiver off the enclosing
/// <see cref="ConditionalAccessExpressionSyntax"/>.
/// <para>
/// Every strategy that destructured only the first shape silently skipped the second, so
/// <c>list?.Contains(x)</c> came out as <c>list?.contains(x)</c> — a method no JS array has — while
/// the same call without the question mark translated correctly. Both shapes name the same call, so
/// they resolve to the same receiver and name here, on the ORIGINAL nodes: the semantic model still
/// answers about them, which a rewritten tree can never do.
/// </para>
/// </summary>
public static class InvocationShapeExtensions
{
    /// <summary>
    /// The receiver and member name of an instance call, in either shape. False for a call that has
    /// no receiver at all (<c>Foo()</c>, a local function).
    /// </summary>
    public static bool TryGetInstanceCall(this InvocationExpressionSyntax invocation,
        out ExpressionSyntax receiver, out SimpleNameSyntax name)
    {
        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax access:
                receiver = access.Expression;
                name = access.Name;
                return true;

            // `a?.M(x)` — and `a?.b.M(x)`, where the chain walks back to the same conditional.
            case MemberBindingExpressionSyntax binding
                when invocation.EnclosingConditionalAccess() is { } conditional:
                receiver = conditional.Expression;
                name = binding.Name;
                return true;

            default:
                receiver = null!;
                name = null!;
                return false;
        }
    }

    /// <summary>
    /// Whether this call is the guarded half of a <c>?.</c> — the caller has to know, because the
    /// receiver it just resolved is only reached when non-null, and the emitted code must keep that.
    /// </summary>
    public static bool IsNullConditional(this InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberBindingExpressionSyntax
        && invocation.EnclosingConditionalAccess() is not null;

    /// <summary>The <c>?.</c> this binding belongs to, walking out through any chained members.</summary>
    private static ConditionalAccessExpressionSyntax? EnclosingConditionalAccess(
        this InvocationExpressionSyntax invocation)
    {
        for (SyntaxNode? node = invocation; node is not null; node = node.Parent)
        {
            if (node is ConditionalAccessExpressionSyntax conditional) return conditional;
            // Stop at anything that is not part of the same access chain.
            if (node.Parent is not (MemberAccessExpressionSyntax or InvocationExpressionSyntax
                or ConditionalAccessExpressionSyntax or MemberBindingExpressionSyntax))
            {
                return null;
            }
        }

        return null;
    }
}
