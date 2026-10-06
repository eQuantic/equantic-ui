using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// <c>Contains</c>, LINQ's and a list's own, by the element type's equality.
/// </summary>
public class ContainsStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        // Both shapes of the same call — `x.Contains(y)` and `x?.Contains(y)`. The second used to
        // fall straight through to the fallback and emit a `.contains` no JS array has.
        if (!invocation.TryGetInstanceCall(out var receiverExpression, out var memberName))
            return false;

        if (memberName.Identifier.Text != "Contains")
            return false;

        // Receiver-type check (most reliable): arrays and sequence types map Contains -> includes.
        // Arrays bind Contains via the Enumerable extension, which the method-symbol checks below
        // miss. HashSet/ISet are intentionally excluded here (they map to Set.has elsewhere).
        var receiverType = context.SemanticHelper.GetType(receiverExpression);
        if (receiverType is IArrayTypeSymbol)
            return true;
        if (receiverType != null)
        {
            if (receiverType.SpecialType == SpecialType.System_String)
                return true;
            var def = receiverType.OriginalDefinition?.ToString() ?? "";
            if (def.StartsWith("System.Collections.Generic.List") ||
                def.StartsWith("System.Collections.Generic.IList") ||
                def.StartsWith("System.Collections.Generic.ICollection") ||
                def.StartsWith("System.Collections.Generic.IReadOnlyList") ||
                // The read-only COLLECTION interface is what an API exposes when it takes a set of
                // keys without promising order — missing it here shipped a `.contains` no JS array
                // has, and the page died at hydration rather than at build.
                def.StartsWith("System.Collections.Generic.IReadOnlyCollection") ||
                def.StartsWith("System.Collections.Generic.IEnumerable"))
                return true;
        }

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms)
        {
            // Accept if it's a LINQ extension OR a collection method (List<T>.Contains, etc.)
            if (context.SemanticHelper.IsLinqExtension(ms.ContainingType))
                return true;

            // Also handle List<T>.Contains, ICollection<T>.Contains, etc.
            var containingType = ms.ContainingType?.ToString();
            if (containingType != null &&
                (containingType.StartsWith("System.Collections.Generic.List") ||
                 containingType.StartsWith("System.Collections.Generic.ICollection") ||
                 containingType.StartsWith("System.Collections.Generic.IEnumerable") ||
                 containingType == "string"))
            {
                return true;
            }
        }

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// <c>Contains</c> by the element type's equality, the one <c>EqualityComparer&lt;T&gt;.Default</c> is
    /// (<see cref="ElementEquality"/>), as a list's <c>IndexOf</c> and <c>Remove</c> take it (#425): a list
    /// held as an array answers with <c>includes</c> where that equality is identity (SameValueZero, NaN
    /// found), and the runtime walks it otherwise, a record, a tuple, a decimal and a class with an
    /// <c>Equals</c> of its own found by value. A receiver whose static type is a mere collection may be
    /// a set when the call runs, which has no <c>includes</c>; the runtime asks the value what it is, and
    /// a set answers by its own equality, as .NET's <c>ICollection&lt;T&gt;.Contains</c> does. A string's
    /// own <c>Contains</c> is its <c>includes</c>.
    /// </summary>
    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        invocation.TryGetInstanceCall(out var receiverExpression, out _);

        var args = invocation.ArgumentList.Arguments;
        var receiverType = context.SemanticHelper.GetType(receiverExpression);
        if (args.Count == 0)
            return $"{context.Converter.ConvertExpression(receiverExpression)}.includes(undefined)";
        var value = context.Converter.ConvertExpression(args[0].Expression);

        if (receiverType?.SpecialType == SpecialType.System_String)
            return $"{context.Converter.ConvertExpression(receiverExpression)}.includes({value})";

        var equality = ElementEquality.Of(receiverType.GetEnumerableElementType());
        // LINQ's Contains reads its source as every operator does (LinqSource): a dictionary as its
        // pairs, which the runtime's class enumerates and has no `includes` for. An array is itself.
        var linq = context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol bound
            && context.SemanticHelper.IsLinqExtension(bound.ContainingType);
        if (equality is null && IsArray(receiverType))
        {
            var source = linq
                ? LinqSource.Text(receiverExpression, context)
                : context.Converter.ConvertExpression(receiverExpression);
            return $"{source}.includes({value})";
        }

        // Anything whose static type is a mere COLLECTION could be a set at run time, and a set has no
        // `includes`: the helper asks the value what it is, so it takes the value as it is, a LINQ call's
        // included, as `Enumerable.Contains` asks a collection its own Contains first. (Under a `?.`
        // this call arrives on the conditional-access strategy's `$r` placeholder, and that strategy
        // wraps the helper in its own null-answering arrow — nothing guarded reaches here in binding
        // shape.)
        context.UsedHelpers.Add(Eq.Import);
        var receiver = context.Converter.ConvertExpression(receiverExpression);
        return equality is null
            ? $"{Eq.Contains}({receiver}, {value})"
            : $"{Eq.Contains}({receiver}, {value}, {equality})";
    }

    /// <summary>Whether a receiver is held as an array on this side whatever it holds: an array, or a
    /// list or one of the faces only a list answers to. A face a set answers to as well is not.</summary>
    private static bool IsArray(ITypeSymbol? type) =>
        type is IArrayTypeSymbol
        || (type is INamedTypeSymbol named && !type.HasOpenCollectionShape()
            && named.OriginalDefinition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
            && named.OriginalDefinition.Name is "List" or "IList" or "IReadOnlyList");

    public int Priority => 10;
}
