using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Null-conditional access — <c>a?.B</c>, <c>a?.M(x)</c>, <c>a?[i]</c>, and every chain hanging
/// off one. ONE mechanism for all of them: the tail is rebuilt with its root binding replaced by
/// an ordinary access on a <c>$r</c> receiver placeholder (<c>?.M(x)</c> → <c>$r.M(x)</c>), which
/// every other strategy already understands, and the rebuilt nodes are mapped to their in-tree
/// originals so the model keeps answering for them — symbols, receiver type, lambda parameters in
/// the arguments. Then the receiver goes back in front: <c>$r.filter(p)</c> becomes
/// <c>a?.filter(p)</c>; a translation that does not START with the placeholder (a helper call, a
/// spread) is wrapped in a null-answering arrow instead.
/// <para>
/// Before this, the guarded shape was its own dialect: <c>?.M(x)</c> went to a camelCase rename
/// because the real strategies only recognised <c>a.M(x)</c> — so <c>text?.ToUpper()</c> shipped
/// as <c>?.toUpper()</c>, <c>items?.Where(p)</c> as <c>?.where(p)</c>, <c>list?.Count</c> as
/// <c>?.count</c>, and a chain behind a guard could even emit <c>a?.this.trim()</c>. No diagnostic
/// for any of it. The rewrite makes the guarded and plain shapes the SAME translation by
/// construction.
/// </para>
/// </summary>
public class ConditionalAccessStrategy : IConversionStrategy
{
    private const string Placeholder = "$r";

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ConditionalAccessExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var conditionalAccess = (ConditionalAccessExpressionSyntax)node;

        // C# 14 null-conditional ASSIGNMENT parses as a conditional access whose WhenNotNull is
        // the assignment itself (`a?.B = v` → ?.(a, ASSIGN(.B, v))) — and JS rejects `?.` on an
        // assignment target outright, so this shape gets its own guarded lowering.
        if (conditionalAccess.WhenNotNull is AssignmentExpressionSyntax conditionalAssignment)
        {
            return NullConditionalAssignment.Convert(conditionalAccess.Expression, conditionalAssignment, context)
                ?? context.Unhandled(node, "null-conditional assignment");
        }
        if (conditionalAccess.WhenNotNull is ConditionalAccessExpressionSyntax assignmentTail
            && CarriesAssignment(assignmentTail))
        {
            return NullConditionalAssignment.ConvertNested(conditionalAccess.Expression, assignmentTail, context)
                ?? context.Unhandled(node, "null-conditional assignment");
        }

        var whenNotNull = conditionalAccess.WhenNotNull;
        var rootBinding = RootBinding(whenNotNull);
        if (rootBinding is null)
            return context.Unhandled(node, "null-conditional access");

        // The guarded member is as bindable as an unguarded one: unbound under an authoritative
        // model is missing references or code that doesn't compile, and the rewritten copy below
        // would otherwise translate by name. Same rule, same code, as the invocation fallback.
        if (rootBinding is MemberBindingExpressionSyntax binding
            && context.SemanticHelper.GetSymbol(binding) is null
            && !context.CanGuess(binding))
        {
            context.Report(node, ConversionSeverity.Error, "EQ2006",
                $"'{binding.Name.Identifier.Text}' does not bind in the compiler's semantic model, so any "
                + "translation would be a guess. Either this code does not compile, or the compiler "
                + "is missing references/generated sources — the SDK passes them via --refs/--generated; "
                + "a custom host must do the same.");
        }

        var receiver = context.Converter.ConvertExpression(conditionalAccess.Expression);
        var rebuilt = Rebuild(whenNotNull, rootBinding, conditionalAccess.Expression, context);

        var converted = context.Converter.ConvertExpression(rebuilt);

        // `$r.filter(p)` → `a?.filter(p)`; `$r[0]` → `a?.[0]`; `$r(x)` (a delegate's Invoke) →
        // `a?.(x)`. Anything not rooted at the placeholder — `$eq.collections.contains($r, x)`,
        // `[...$r, x]` — is wrapped so the receiver is still evaluated once and null still answers null.
        if (converted.StartsWith(Placeholder + ".", StringComparison.Ordinal))
            return $"{receiver}?.{converted[(Placeholder.Length + 1)..]}";
        if (converted.StartsWith(Placeholder + "[", StringComparison.Ordinal)
            || converted.StartsWith(Placeholder + "(", StringComparison.Ordinal))
            return $"{receiver}?.{converted[Placeholder.Length..]}";
        return $"(({Placeholder}) => {Placeholder} == null ? null : {converted})({receiver})";
    }

    /// <summary>The leftmost binding of the tail — the `.B` of `?.B.C(x)`, the `[i]` of `?[i]` —
    /// which is where the receiver is implicitly attached. Null for a tail this does not model.</summary>
    private static ExpressionSyntax? RootBinding(ExpressionSyntax tail)
    {
        for (ExpressionSyntax current = tail; ;)
        {
            switch (current)
            {
                case MemberBindingExpressionSyntax or ElementBindingExpressionSyntax:
                    return current;
                case InvocationExpressionSyntax invocation:
                    current = invocation.Expression;
                    continue;
                case MemberAccessExpressionSyntax access:
                    current = access.Expression;
                    continue;
                case ElementAccessExpressionSyntax element:
                    current = element.Expression;
                    continue;
                case ConditionalAccessExpressionSyntax nested:
                    current = nested.Expression;
                    continue;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// The tail with its root binding replaced by an access on the `$r` placeholder, every rebuilt
    /// node mapped to its original (Roslyn's TrackNodes survives the ReplaceNode, so the mapping is
    /// exact), and the placeholder carrying the receiver's TYPE so shape-dependent translations
    /// (`.Count` on a Set, `Contains` on an open collection) still see what they need.
    /// </summary>
    private static ExpressionSyntax Rebuild(ExpressionSyntax tail, ExpressionSyntax rootBinding,
        ExpressionSyntax receiverSyntax, ConversionContext context)
    {
        var originals = tail.DescendantNodesAndSelf().ToArray();
        var tracked = tail.TrackNodes(originals);
        var trackedRoot = tracked.GetCurrentNode(rootBinding)!;

        var placeholder = SyntaxFactory.IdentifierName(Placeholder);
        SyntaxNode replacement = trackedRoot switch
        {
            MemberBindingExpressionSyntax member => SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression, placeholder, member.Name),
            ElementBindingExpressionSyntax element => SyntaxFactory.ElementAccessExpression(
                placeholder, element.ArgumentList),
            _ => throw new InvalidOperationException("root binding shape"),
        };
        var rebuilt = tracked.ReplaceNode(trackedRoot, replacement);

        foreach (var original in originals)
        {
            if (rebuilt.GetCurrentNode(original) is { } current)
                context.SemanticHelper.MapSynthetic(current, original);
        }

        // The replacement itself is untracked: find it through the placeholder and map it to the
        // binding it replaced, so `GetSymbol(memberAccess)` answers the member's symbol.
        var placed = rebuilt.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .First(identifier => identifier.Identifier.Text == Placeholder);
        if (placed.Parent is { } access) context.SemanticHelper.MapSynthetic(access, rootBinding);
        context.SemanticHelper.MapType(placed, context.SemanticHelper.GetType(receiverSyntax));

        return rebuilt;
    }

    /// <summary>Whether a nested <c>?.</c> chain ultimately carries an assignment (`a?.b?.c = v`).</summary>
    private static bool CarriesAssignment(ConditionalAccessExpressionSyntax tail) =>
        tail.WhenNotNull switch
        {
            AssignmentExpressionSyntax => true,
            ConditionalAccessExpressionSyntax deeper => CarriesAssignment(deeper),
            _ => false,
        };

    public int Priority => 15; // Higher priority to intercept before MemberAccessStrategy
}
