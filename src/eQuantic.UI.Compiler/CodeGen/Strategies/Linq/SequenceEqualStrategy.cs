using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// <c>SequenceEqual(second)</c> and <c>SequenceEqual(second, comparer)</c>, LINQ's and the one an array
/// binds through <c>MemoryExtensions</c>, through the runtime's <c>$eq.linq.sequenceEqual</c>: element by
/// element by the element type's default equality (<see cref="ElementEquality"/>), as .NET compares
/// (#721). The text of each sequence's JSON was compared instead, which threw for a long, told
/// <c>1.0m</c> from <c>1.00m</c>, took a null for a NaN and two arrays of the same items for one, and the
/// overload with a comparer answered its first sequence. A comparer is the collection fence's to judge:
/// one that asks for the default equality is dropped, any other is refused (EQ2007). A call no model
/// binds keeps the JSON comparison.
/// </summary>
public class SequenceEqualStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.ValueText != "SequenceEqual") return false;

        // An ARRAY receiver binds MemoryExtensions.SequenceEqual (through ReadOnlySpan) rather
        // than Enumerable's — a better overload the modern BCL added — and the LINQ gate alone
        // then refused the call, leaving `MemoryExtensions.sequenceEqual(…)`, a name that exists
        // nowhere. Both spell the same element-wise comparison.
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms
            && (context.SemanticHelper.IsLinqExtension(ms.ContainingType)
                || ms.ContainingType?.Name == "MemoryExtensions")) return true;

        return symbol == null && context.CanGuess(node);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
            return ByTheDefaultEquality(invocation, memberAccess, method, context);

        var caller = LinqSource.Text(memberAccess.Expression, context);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count == 1)
        {
            var other = Ir.JsExprWriter.Write(LinqSource.Argument(args[0], invocation, context));
            // JSON.stringify approach for simplicity in UI context
            return $"(JSON.stringify({caller}) === JSON.stringify({other}))";
        }

        return caller;
    }

    /// <summary>
    /// The comparison by the element type's default equality, its comparer judged first. The parameters are
    /// LINQ's (<c>second</c>, <c>comparer</c>) or the span's (<c>other</c>, <c>comparer</c>); a span is
    /// what a null array converts to, an empty one, so that form reads a null as no element where LINQ
    /// refuses it by name.
    /// </summary>
    private static string ByTheDefaultEquality(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax access,
        IMethodSymbol method, ConversionContext context)
    {
        if (method.Parameters.Length > 0 && method.Parameters[^1].Type.IsEqualityComparer())
        {
            var written = invocation.ArgumentList.Arguments;
            var names = LinqArguments.Parameters(invocation, context);
            var at = names is null ? -1 : Array.IndexOf([.. names], "comparer");
            if (at >= 0 && context.SemanticHelper.GetOperation(written[at].Expression) is { } comparer
                && comparer.RefusesAsUntranslatable("SequenceEqual", context))
                return invocation.ToString();
        }

        var spans = method.ContainingType?.Name == "MemoryExtensions";
        var other = spans ? "other" : "second";
        var equality = ElementEquality.Of(method.TypeArguments.FirstOrDefault());
        string[] settled = spans ? [equality ?? "false", "true"] : equality is null ? [] : [equality];
        var call = LinqArguments.Call(Eq.LinqSequenceEqual, LinqSource.Ir(access.Expression, context), [other], invocation,
            argument => LinqSource.Argument(argument, invocation, context), context, settled, dropped: "comparer");
        return call is null ? context.Unhandled(invocation, "SequenceEqual") : Ir.JsExprWriter.Write(call);
    }

    public int Priority => 10;
}
