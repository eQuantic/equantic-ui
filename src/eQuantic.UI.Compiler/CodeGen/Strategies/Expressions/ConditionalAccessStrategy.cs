using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Null-conditional access — <c>a?.B</c>, <c>a?.M(x)</c>, <c>a?[i]</c>, and every chain hanging
/// off one. ONE mechanism for all of them: the tail is rebuilt with its root binding replaced by
/// an ordinary access on a receiver placeholder, a temporary of its own (<c>?.M(x)</c> →
/// <c>$n0.M(x)</c>), which every other strategy already understands, and the rebuilt nodes are
/// mapped to their in-tree originals so the model keeps answering for them — symbols, receiver
/// type, lambda parameters in the arguments. Then the receiver goes back in front:
/// <c>$n0.filter(p)</c> becomes <c>a?.filter(p)</c>. A translation that does not START with the
/// placeholder (a helper call, a spread) tests the receiver it assigns to the temporary,
/// <c>(($n0 = a) == null ? null : $eq.collections.contains($n0, x))</c>, and the statement it
/// stands in declares the temporary (#539).
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
        var readsAgain = ReadsAgain(conditionalAccess.Expression, context);
        // A temporary of its own, so a `?.` in the tail of this one binds another.
        var placeholder = readsAgain ? receiver : context.Temporaries.Fresh();
        var rebuilt = Rebuild(whenNotNull, rootBinding, conditionalAccess.Expression, readsAgain, placeholder, context);

        var converted = context.Converter.ConvertExpression(rebuilt);

        // `$n0.filter(p)` → `a?.filter(p)`; `$n0[0]` → `a?.[0]`; `$n0(x)` (a delegate's Invoke) →
        // `a?.(x)`. Anything not rooted at the receiver — `$eq.collections.contains($n0, x)`,
        // `[...$n0, x]` — is guarded so the receiver is still evaluated once and null still answers null.
        if (converted.StartsWith(placeholder + ".", StringComparison.Ordinal))
            return AnswersNull(conditionalAccess, $"{receiver}?.{converted[(placeholder.Length + 1)..]}", context);
        if (converted.StartsWith(placeholder + "[", StringComparison.Ordinal)
            || converted.StartsWith(placeholder + "(", StringComparison.Ordinal))
            return AnswersNull(conditionalAccess, $"{receiver}?.{converted[placeholder.Length..]}", context);
        // No function around the tail: it runs in the function it is written in, so an argument that
        // awaits is awaited there, only when the receiver is not null, and nothing the call answers
        // is awaited (an arrow made async did both, and a task the call returned came back as its
        // result, #536). A receiver read again is tested where it is; any other is assigned to the
        // temporary the statement around it declares (#539).
        if (readsAgain)
            return $"({receiver} == null ? null : {converted})";
        if (context.Temporaries.CanBind)
        {
            context.Temporaries.Bind(placeholder);
            return $"(({placeholder} = {receiver}) == null ? null : {converted})";
        }
        // Nothing is open to declare one: an initializer, which runs outside any statement. C# lets
        // no initializer await, so the arrow that binds the receiver there runs once and never
        // suspends; a tail that awaits anywhere else would have a statement around it.
        if (AwaitsInItsOwnBody(whenNotNull))
            return context.Unhandled(node,
                "null-conditional access (an argument that awaits, where no statement can declare the receiver's temporary)");
        return $"(({placeholder}) => {placeholder} == null ? null : {converted})({receiver})";
    }

    /// <summary>
    /// JavaScript's optional chain answers <c>undefined</c> where C# answers <c>null</c>, and the two
    /// part ways where the value is used: a parameter typed <c>T | null</c> refuses it, JSON drops the
    /// key that holds it, and <c>=== null</c> is false for it (#633). So the chain answers null, except
    /// where nothing can tell: a call that returns nothing (<c>onChanged?.Invoke(x)</c>, whose value C#
    /// never lets anyone use), a statement that discards its value, the left of a <c>??</c>, and the
    /// tail of another chain, whose own answer is settled where it ends.
    /// </summary>
    private static string AnswersNull(ConditionalAccessExpressionSyntax access, string chain, ConversionContext context)
    {
        if (context.SemanticHelper.GetType(access) is { SpecialType: SpecialType.System_Void }) return chain;
        SyntaxNode node = access;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized) node = parenthesized;
        var settled = node.Parent switch
        {
            null => true,
            ExpressionStatementSyntax => true,
            ConditionalAccessExpressionSyntax => true,
            BinaryExpressionSyntax coalesce when coalesce.IsKind(SyntaxKind.CoalesceExpression) && coalesce.Left == node => true,
            _ => false,
        };
        return settled ? chain : $"({chain} ?? null)";
    }

    /// <summary>A receiver whose second read nobody can observe, and that nothing between the null
    /// test and the read can change: a local, a parameter, or <c>this</c>.</summary>
    private static bool ReadsAgain(ExpressionSyntax receiver, ConversionContext context) =>
        receiver is ThisExpressionSyntax
        || receiver is IdentifierNameSyntax
            && context.SemanticHelper.GetSymbol(receiver) is ILocalSymbol or IParameterSymbol;

    /// <summary>Whether the tail awaits in the function it is written in: an <c>await</c> inside a
    /// lambda of its own belongs to that lambda, which is async on its own account.</summary>
    private static bool AwaitsInItsOwnBody(ExpressionSyntax tail) =>
        tail.DescendantNodesAndSelf(node => node is not AnonymousFunctionExpressionSyntax)
            .OfType<AwaitExpressionSyntax>()
            .Any();

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
    /// The tail with its root binding replaced by an access on the receiver's placeholder (the
    /// temporary <paramref name="placeholderName"/> names), every rebuilt node mapped to its
    /// original (Roslyn's TrackNodes survives the ReplaceNode, so the mapping is exact), and the
    /// placeholder carrying the receiver's TYPE so shape-dependent translations (`.Count` on a Set,
    /// `Contains` on an open collection) still see what they need. A receiver that is read again
    /// takes the placeholder's place itself, mapped to the receiver it copies.
    /// </summary>
    private static ExpressionSyntax Rebuild(ExpressionSyntax tail, ExpressionSyntax rootBinding,
        ExpressionSyntax receiverSyntax, bool readsAgain, string placeholderName, ConversionContext context)
    {
        var originals = tail.DescendantNodesAndSelf().ToArray();
        var tracked = tail.TrackNodes(originals);
        var trackedRoot = tracked.GetCurrentNode(rootBinding)!;

        // Found again by annotation: the receiver's own name may also be written in the tail.
        var marker = new SyntaxAnnotation();
        ExpressionSyntax placeholder = (readsAgain
            ? receiverSyntax.WithoutTrivia()
            : SyntaxFactory.IdentifierName(placeholderName)).WithAdditionalAnnotations(marker);
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

        // The replacement itself is untracked: find it through its annotation and map it to the
        // binding it replaced, so `GetSymbol(memberAccess)` answers the member's symbol.
        var placed = rebuilt.GetAnnotatedNodes(marker).First();
        if (placed.Parent is { } access) context.SemanticHelper.MapSynthetic(access, rootBinding);
        if (readsAgain) context.SemanticHelper.MapSynthetic(placed, receiverSyntax);
        else context.SemanticHelper.MapType(placed, context.SemanticHelper.GetType(receiverSyntax));

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
