using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for <c>OrderBy</c>/<c>OrderByDescending</c> and their <c>ThenBy</c>/<c>ThenByDescending</c>
/// continuations. A whole ordering chain (<c>src.OrderBy(k1).ThenBy(k2).ThenByDescending(k3)</c>) is
/// collapsed into a SINGLE stable sort with a composite comparator — comparing by each key in turn,
/// honouring each key's direction. (Re-sorting per key would be wrong: a stable sort by the secondary
/// key alone keeps the primary order only among secondary-equal items, inverting the precedence.)
/// The source is copied first ([...src]) so the original sequence is never mutated, matching LINQ; JS's
/// stable sort makes items equal on all keys keep their input order, matching LINQ's stable ordering.
/// </summary>
public class OrderByStrategy : IConversionStrategy
{
    private static readonly HashSet<string> OrderMethods = new()
    {
        "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending",
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
            && OrderMethods.Contains(access.Name.Identifier.Text);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // Walk the ordering chain from outermost (this node) inward, collecting each key selector and
        // its direction; the deepest receiver is the base source.
        var keys = new List<(string Selector, bool Descending)>();
        ExpressionSyntax current = (InvocationExpressionSyntax)node;
        ExpressionSyntax source = current;

        while (current is InvocationExpressionSyntax inv
               && inv.Expression is MemberAccessExpressionSyntax acc
               && OrderMethods.Contains(acc.Name.Identifier.Text))
        {
            var method = acc.Name.Identifier.Text;
            if (inv.ArgumentList.Arguments.Count > 0)
            {
                var selector = context.Converter.ConvertExpression(inv.ArgumentList.Arguments[0].Expression);
                var descending = method.EndsWith("Descending");
                keys.Insert(0, (selector, descending)); // outer call = later (lower-priority) key
            }
            source = acc.Expression;
            current = acc.Expression;

            // An OrderBy is a PRIMARY key: it does not refine the ordering under it, it REPLACES
            // it. `xs.OrderBy(a).OrderBy(b)` sorts by b, and the a-ordering survives only as the
            // tiebreak a stable sort gives it — so the chain is CUT here and what is under it
            // becomes the source, which sorts itself. Composing both keys into one comparator is
            // what ThenBy means, and it silently produced a different order.
            if (!method.StartsWith("ThenBy", StringComparison.Ordinal)) break;
        }

        var src = context.Converter.ConvertExpression(source);
        if (keys.Count == 0) return $"[...{src}].sort()";

        var body = new System.Text.StringBuilder();
        foreach (var (selector, descending) in keys)
        {
            var lt = descending ? "1" : "-1";
            var gt = descending ? "-1" : "1";
            body.Append($"{{ const _k = {selector}; const _a = _k(a), _b = _k(b); ");
            body.Append($"if (_a < _b) return {lt}; if (_a > _b) return {gt}; }} ");
        }
        body.Append("return 0;");

        return $"[...{src}].sort((a, b) => {{ {body} }})";
    }

    public int Priority => 10;
}
