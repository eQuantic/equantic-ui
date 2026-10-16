using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// LINQ's <c>Distinct()</c>: the runtime's set over the elements, which finds them as the element type's
/// default comparer does, or a JavaScript <c>Set</c> for a type compared by identity.
/// </summary>
public class DistinctStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.ValueText != "Distinct")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
        {
            return true;
        }

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
        {
            return true;
        }

        return false;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        // A comparer is the collection fence's to judge (#578): one that asks for the element type's own
        // equality, which the shapes below already are, is dropped, and any other is refused, the call
        // written as its own C# text, as ToHashSet's is. It was dropped whatever it asked for, so
        // `Distinct(StringComparer.OrdinalIgnoreCase)` kept "a" and "A" in the browser alone.
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (context.SemanticHelper.GetOperation(argument.Expression) is not { } comparer)
                return JsExpr.Opaque(context.Unhandled(invocation, "Distinct with a comparer"));
            if (comparer.RefusesAsUntranslatable("Distinct", context)) return JsExpr.Opaque(invocation.ToString());
        }

        var source = LinqSource.Ir(memberAccess.Expression, context);

        // An element is found as its type's default comparer finds it (ElementEquality): a record, a struct,
        // a decimal and a date by value, an anonymous object and a tuple by their members, an array they
        // hold by reference, and a class by its own Equals where it overrides one. The runtime's set keeps
        // the first of each, in the order they come, as Distinct does. A Set kept every decimal, every
        // anonymous object and every class that overrides Equals, and JSON.stringify found two records
        // equal by the text of every member, an array they held included.
        var elementType = context.SemanticHelper.GetType(memberAccess.Expression).GetEnumerableElementType();
        if (ElementEquality.Of(elementType) is { } equality)
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Template($"[...{Eq.HashSet}({equality}, {{0}})]", [source]);
        }

        // A type compared by identity, which a Set compares as .NET does, a NaN equal to itself included
        // (SameValueZero).
        return JsExpr.Template("[...new Set({0})]", [source]);
    }

    public int Priority => 10;
}
