using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The three LINQ sequences that come from NOTHING rather than from another sequence —
/// <c>Enumerable.Range</c>, <c>Repeat</c> and <c>Empty</c>. Every other LINQ operator had a strategy
/// because it hangs off a receiver; these are statics, so they fell through to the fallback and were
/// emitted as <c>Enumerable.range(…)</c> — a name the browser has never heard of.
/// <para>
/// A sequence IS a JS array here (that is what every other LINQ strategy assumes), so these
/// materialise directly instead of going through a helper: <c>Array.from</c> with a length is the
/// same one-pass construction .NET does lazily, and the result is what the next operator expects.
/// </para>
/// </summary>
public class EnumerableFactoryStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var receiver = memberAccess.Expression.ToString();
        if (receiver is not ("Enumerable" or "System.Linq.Enumerable")) return false;

        return Name(memberAccess) is "Range" or "Repeat" or "Empty";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToList();

        return Name(memberAccess) switch
        {
            "Range" when args.Count == 2 =>
                $"Array.from({{ length: {args[1]} }}, (_v, _i) => {args[0]} + _i)",
            "Repeat" when args.Count == 2 =>
                $"Array.from({{ length: {args[1]} }}, () => {args[0]})",
            "Empty" => "[]",
            _ => "[]",
        };
    }

    /// <summary>The member name, with any type argument (<c>Empty&lt;string&gt;</c>) set aside.</summary>
    private static string Name(MemberAccessExpressionSyntax memberAccess) =>
        memberAccess.Name.Identifier.Text;

    public int Priority => 12;
}
