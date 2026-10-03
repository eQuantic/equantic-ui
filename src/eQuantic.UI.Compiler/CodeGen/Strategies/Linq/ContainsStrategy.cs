using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Contains(item) to JavaScript .includes(item)
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

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        invocation.TryGetInstanceCall(out var receiverExpression, out _);

        // LINQ's Contains reads its source as every operator does (LinqSource): a dictionary as its
        // pairs, which the runtime's class enumerates and has no `some` or `includes` for. A
        // collection's own Contains (a list's, a string's) stays its own.
        var linq = context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol bound
            && context.SemanticHelper.IsLinqExtension(bound.ContainingType);
        string Caller() => linq
            ? LinqSource.Text(receiverExpression, context)
            : context.Converter.ConvertExpression(receiverExpression);
        var args = invocation.ArgumentList.Arguments;
        var receiverType = context.SemanticHelper.GetType(receiverExpression);

        if (args.Count > 0)
        {
            // Records / structs / tuples compare by VALUE — `includes` (SameValueZero) would miss
            // equal-but-distinct instances. Walk with the structural equality helper instead.
            if (receiverType.GetEnumerableElementType().IsStructuralValueType())
            {
                var caller = Caller();
                context.UsedHelpers.Add(Eq.Import);
                return $"{caller}.some(_x => {Eq.Equals}(_x, {context.Converter.ConvertExpression(args[0].Expression)}))";
            }

            // Anything whose static type is a mere COLLECTION could be a HashSet at run time, and
            // a Set has no `includes` — the call returns undefined and the selection silently never
            // matches. The helper asks the value what it is, so it takes the value as it is, a LINQ
            // call's included: `Enumerable.Contains` asks a collection its own Contains first too.
            // (Under a `?.` this call arrives on the conditional-access strategy's `$r` placeholder,
            // and that strategy wraps the helper in its own null-answering arrow — nothing guarded
            // reaches here in binding shape.)
            if (receiverType.HasOpenCollectionShape())
            {
                var value = context.Converter.ConvertExpression(receiverExpression);
                context.UsedHelpers.Add(Eq.Import);
                return $"{Eq.Contains}({value}, {context.Converter.ConvertExpression(args[0].Expression)})";
            }

            var source = Caller();
            return $"{source}.includes({context.Converter.ConvertExpression(args[0].Expression)})";
        }

        return $"{Caller()}.includes(undefined)";
    }

    public int Priority => 10;
}
