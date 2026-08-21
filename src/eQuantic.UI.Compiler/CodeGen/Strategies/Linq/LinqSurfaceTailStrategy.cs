using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The modern tail of the Enumerable surface — <c>Append</c>, <c>SkipLast</c>, <c>ToHashSet</c>,
/// <c>Order</c>, <c>ExceptBy</c>… — one symbol-first, table-driven strategy instead of thirteen
/// more single-operator files. Same doctrine as PrimitiveStaticStrategy: a member translates only
/// when the table names it AND the symbol binds to System.Linq, every argument evaluates exactly
/// once (receiver-reusing shapes go through an arrow), and what the table doesn't name stays
/// visibly fenced in the audit baseline (<c>Shuffle</c> is RANDOM, <c>Index</c> yields tuples —
/// deliberately absent).
/// <para>
/// <c>Order</c>/<c>OrderDescending</c> use the generic ordinal comparator — culture-sensitive
/// string ordering is out of scope by standing policy (see the runtime's sorted.ts), exactly as
/// OrderBy already behaves.
/// </para>
/// </summary>
public class LinqSurfaceTailStrategy : IExpressionIrStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (!invocation.TryGetInstanceCall(out _, out var name)) return false;
        if (Template(name.Identifier.Text, invocation.ArgumentList.Arguments.Count) is null) return false;

        return context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method
            && context.SemanticHelper.IsLinqExtension(method.ContainingType);
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        invocation.TryGetInstanceCall(out var receiverSyntax, out var name);

        var receiver = context.Converter.ConvertIr(receiverSyntax);
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertIr(a.Expression))
            .ToArray();

        var template = Template(name.Identifier.Text, args.Length)!;
        if (template.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);

        // {0} is the receiver; {1}… the arguments. The writer binds whatever is reused.
        return JsExpr.Template(template, new[] { receiver }.Concat(args).ToArray(), context.TypeAnnotations);
    }

    private static string? Template(string name, int argCount) => (name, argCount) switch
    {
        ("Append", 1) => "[...{0}, {1}]",
        ("Prepend", 1) => "[{1}, ...{0}]",
        ("AsEnumerable", 0) => "{0}",
        ("ToHashSet", 0) => "new Set({0})",
        // A long is a BigInt on this side — Count already answers as a number, LongCount must not.
        ("LongCount", 0) => $"{Eq.Long}({{0}}.length)",
        ("LongCount", 1) => $"{Eq.Long}({{0}}.filter({{1}}).length)",
        // SkipLast(0)/TakeLast(0) are the traps: slice(0, -0) is the EMPTY prefix and slice(-0)
        // the WHOLE array — computing the start explicitly sidesteps both.
        ("SkipLast", 1) => "{0}.slice(0, Math.max(0, {0}.length - {1}))",
        ("TakeLast", 1) => "({1} > 0 ? {0}.slice(Math.max(0, {0}.length - {1})) : [])",
        // Ordinal comparator, ascending/descending — the standing culture policy.
        ("Order", 0) => "[...{0}].sort(($a, $b) => $a < $b ? -1 : $a > $b ? 1 : 0)",
        ("OrderDescending", 0) => "[...{0}].sort(($a, $b) => $a < $b ? 1 : $a > $b ? -1 : 0)",
        // The *By set operators: distinct-by-key semantics, second operand is the KEY sequence for
        // ExceptBy/IntersectBy and the same-shaped sequence for UnionBy.
        ("ExceptBy", 2) =>
            "(($a, $b, $k) => { const $s = new Set($b); const $r = []; for (const $x of $a) { const $key = $k($x); if (!$s.has($key)) { $s.add($key); $r.push($x); } } return $r; })({0}, {1}, {2})",
        ("IntersectBy", 2) =>
            "(($a, $b, $k) => { const $s = new Set($b); const $seen = new Set(); const $r = []; for (const $x of $a) { const $key = $k($x); if ($s.has($key) && !$seen.has($key)) { $seen.add($key); $r.push($x); } } return $r; })({0}, {1}, {2})",
        ("UnionBy", 2) =>
            "(($a, $b, $k) => { const $seen = new Set(); const $r = []; for (const $x of [...$a, ...$b]) { const $key = $k($x); if (!$seen.has($key)) { $seen.add($key); $r.push($x); } } return $r; })({0}, {1}, {2})",
        _ => null,
    };
}
