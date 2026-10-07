using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for <c>OrderBy</c>/<c>OrderByDescending</c> and their <c>ThenBy</c>/<c>ThenByDescending</c>
/// continuations. A whole ordering chain (<c>src.OrderBy(k1).ThenBy(k2).ThenByDescending(k3)</c>) is
/// collapsed into a SINGLE stable sort with a composite comparator — comparing by each key in turn,
/// honouring each key's direction. (Re-sorting per key would be wrong: a stable sort by the secondary
/// key alone keeps the primary order only among secondary-equal items, inverting the precedence.)
/// The source is copied first ([...src]) so the original sequence is never mutated, matching LINQ; JS's
/// stable sort makes items equal on all keys keep their input order, matching LINQ's stable ordering.
/// <para>
/// The comparator's names take a `$`, which no C# name holds: it was <c>(a, b)</c>, and a key selector
/// that read a captured <c>a</c> read the element being compared instead (#397). A selector that is not
/// a lambda is evaluated once, before the sort runs, where it ran on every comparison.
/// </para>
/// </summary>
public class OrderByStrategy : IExpressionIrStrategy
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

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        // Walk the ordering chain from outermost (this node) inward, collecting each key selector and
        // its direction; the deepest receiver is the base source.
        var keys = new List<(JsExpr Selector, bool Descending)>();
        ExpressionSyntax current = (InvocationExpressionSyntax)node;
        ExpressionSyntax source = current;

        while (current is InvocationExpressionSyntax inv
               && inv.Expression is MemberAccessExpressionSyntax acc
               && OrderMethods.Contains(acc.Name.Identifier.Text))
        {
            var method = acc.Name.Identifier.Text;
            if (inv.ArgumentList.Arguments.Count > 0)
            {
                var selector = context.Converter.ConvertIr(inv.ArgumentList.Arguments[0].Expression);
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

        // Through the one place every operator reads its source: a string spread by code point, and
        // it was spread here, where .NET sorts its chars.
        var src = LinqSource.Ir(source, context);
        if (keys.Count == 0) return JsExpr.Template("[...{0}].sort()", [src]);

        // The key selector is typed through the comparator's own parameter, which the sorted array
        // types. Alone in `const $k = (filler) => …` nothing gave the lambda a type, and the
        // runtime's own build refused the implicit any; plain JavaScript carries no annotation.
        var keyType = context.TypeAnnotations ? ": ($element: typeof $a) => any" : "";
        var body = new System.Text.StringBuilder();
        for (var i = 0; i < keys.Count; i++)
        {
            var lt = keys[i].Descending ? "1" : "-1";
            var gt = keys[i].Descending ? "-1" : "1";
            body.Append($"{{ const $k{keyType} = {{{i + 1}}}; const $x = $k($a), $y = $k($b); ");
            body.Append($"if ($x < $y) return {lt}; if ($x > $y) return {gt}; }} ");
        }
        body.Append("return 0;");

        // {0} is the source and {1}… the key selectors, in the order C# evaluates them: the writer
        // binds a selector that is not a lambda, which the comparator would otherwise run each time.
        return JsExpr.Template($"[...{{0}}].sort(($a, $b) => {{ {body} }})",
            [src, .. keys.Select(key => key.Selector)], context.TypeAnnotations);
    }

    public int Priority => 10;
}
