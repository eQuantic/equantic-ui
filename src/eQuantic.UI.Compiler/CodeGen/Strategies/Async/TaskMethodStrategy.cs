using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Async;

/// <summary>
/// Strategy for Task static methods.
/// Handles:
/// - Task.Delay(ms) -> Promise delay
/// - Task.Run(fn) -> Promise wrapper
/// - Task.WhenAll(tasks) -> Promise.all
/// </summary>
public class TaskMethodStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // `Task.CompletedTask` is a PROPERTY, so the invocation gate below never saw it and it went
        // out as `Task.completedTask`. The guard on Parent is what keeps this from also matching
        // the callee of every call: `Task.Delay(1)` has a member access inside it too, and it is
        // the invocation that owns the translation.
        if (node is MemberAccessExpressionSyntax property)
            return property.Parent is not InvocationExpressionSyntax
                && IsTaskType(property.Expression.ToString())
                && property.Name.Identifier.Text == "CompletedTask";

        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        // Heuristic check for "Task"
        var expr = memberAccess.Expression.ToString();
        var name = memberAccess.Name.Identifier.Text;
        
        // ConfigureAwait is an INSTANCE call on a task, so the `Task.` gate below never saw it and
        // it went out as `.configureAwait(false)` — a method that exists nowhere. JavaScript has no
        // synchronization context to opt out of, so the honest translation is to DROP it and keep
        // the receiver. It is everywhere in real async C#, and it broke the module at parse time.
        //
        // Gated on the SYMBOL, not the name. A name gate takes any method called ConfigureAwait,
        // including one somebody wrote on their own type, and drops the call silently — which is
        // the failure this whole strategy exists to stop. The name is consulted only where the
        // model cannot answer, which is the rule everywhere else in here.
        if (name == "ConfigureAwait")
            return IsAwaitableConfigure(invocation, context) || context.CanGuess(invocation);

        if (!IsTaskType(expr)) return false;

        return name is "Delay" or "Run" or "WhenAll" or "WhenAny" or "FromResult" or "Yield";
    }

    /// <summary>Whether this <c>ConfigureAwait</c> is the BCL's, on a task or a value task.</summary>
    private static bool IsAwaitableConfigure(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol method) return false;
        var owner = method.ContainingType?.ConstructedFrom ?? method.ContainingType;
        var name = owner?.ToDisplayString();
        return name is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task<TResult>"
            or "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask<TResult>";
    }

    private static bool IsTaskType(string expression) =>
        expression is "Task" or "System.Threading.Tasks.Task";

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // An already-completed task is a resolved promise: awaiting it yields immediately, which
        // is what `await Task.CompletedTask` means on the other side too.
        if (node is MemberAccessExpressionSyntax) return "Promise.resolve()";

        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;

        // `t.ConfigureAwait(false)` IS `t` here: the flag says which context to resume on, and
        // there is only one. Dropping the call drops its ARGUMENT too, which is only safe while
        // the argument cannot do anything — a constant. Anything else is refused rather than
        // silently discarded: a side effect that stops happening is the kind of defect that shows
        // up nowhere near the line that caused it.
        if (name == "ConfigureAwait")
        {
            var flag = args.Count > 0 ? args[0].Expression : null;
            if (flag is not null && !context.SemanticHelper.TryGetConstantValue(flag, out _))
            {
                context.Report(node, ConversionSeverity.Error, "EQ2113",
                    "'ConfigureAwait' has no JavaScript translation — there is one context to "
                    + "resume on — so the call is dropped, and dropping it would discard this "
                    + "argument without evaluating it. Pass a constant, or evaluate the expression "
                    + "into a local first.");
                return context.Converter.ConvertExpression(memberAccess.Expression);
            }

            return context.Converter.ConvertExpression(memberAccess.Expression);
        }

        if (name == "Yield")
        {
            // `Task.Yield()` gives the scheduler a turn: the continuation is GUARANTEED to run
            // asynchronously. A resolved promise is a microtask and would not let anything else in
            // — setTimeout(0) is the analogue that actually yields the loop, the same shape Delay
            // already uses. Untranslated it emitted `Task.yield()`, a name that exists nowhere, and
            // the module died at the first call.
            return "new Promise(resolve => setTimeout(resolve, 0))";
        }

        if (name == "Delay")
        {
            var ms = context.Converter.ConvertExpression(args[0].Expression);
            return $"new Promise(resolve => setTimeout(resolve, {ms}))";
        }
        
        if (name == "Run")
        {
            var fn = context.Converter.ConvertExpression(args[0].Expression);
            // Task.Run(() => ...) -> Promise.resolve().then(() => ...)
            // Or just execute immediately inside promise to offload? JS is single threaded event loop.
            // Usually Promise.resolve().then(fn) works best to schedule microtask.
            return $"Promise.resolve().then({fn})";
        }
        
        if (name == "WhenAll")
            return Combinator("Promise.all", invocation, args, context);
        
        if (name == "WhenAny")
            return Combinator("Promise.race", invocation, args, context);
        
        if (name == "FromResult")
        {
            var val = context.Converter.ConvertExpression(args[0].Expression);
            return $"Promise.resolve({val})";
        }

        return context.Unhandled(node, "Task");
    }

    /// <summary>
    /// <c>Promise.all</c> / <c>Promise.race</c>, which take an ITERABLE. Several arguments are a
    /// list of tasks and become an array; ONE argument is ambiguous — <c>WhenAll(t)</c> passes a
    /// single task and <c>WhenAll(list)</c> passes the sequence, and the old code assumed the
    /// second. <c>Promise.race(aPromise)</c> throws, because a promise is not iterable, so
    /// <c>await Task.WhenAny(F(1))</c> died at runtime.
    /// <para>
    /// The MODEL decides which it is, since the shapes are indistinguishable in syntax. Where it
    /// cannot answer, the argument is wrapped: a single task is the common form by far, and an
    /// array of one sequence is at least a value <c>Promise.all</c> accepts rather than a throw.
    /// </para>
    /// </summary>
    private static string Combinator(
        string js,
        InvocationExpressionSyntax invocation,
        SeparatedSyntaxList<ArgumentSyntax> args,
        ConversionContext context)
    {
        var translated = args.Select(a => context.Converter.ConvertExpression(a.Expression)).ToList();
        if (translated.Count != 1) return $"{js}([{string.Join(", ", translated)}])";

        var type = context.SemanticHelper.GetType(args[0].Expression);
        var isSequence = type is not null
            && type.SpecialType != SpecialType.System_String
            && type.AllInterfaces.Any(i => i.SpecialType == SpecialType.System_Collections_IEnumerable);

        return isSequence ? $"{js}({translated[0]})" : $"{js}([{translated[0]}])";
    }

    public int Priority => 10;
}
