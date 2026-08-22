using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The LINQ surface as a TABLE: one entry per (operator, argument count), giving the JavaScript
/// shape and nothing else. The gate — an invocation through a member access, bound by the model to
/// a LINQ extension, or claimed by name only where there is no model to ask — is written once
/// here, which is what forty individual strategies were each repeating around two lines of shape.
/// <para>
/// The shape is a <see cref="JsExpr.Template(string, IReadOnlyList{JsExpr})"/>, so a receiver used
/// twice is bound to a temporary and evaluated once, and the IR writer punctuates: an entry cannot
/// forget a parenthesis or evaluate a source twice, because it never writes either.
/// </para>
/// <para>
/// An operator that has to REASON — about the element type (the OrDefault family), the semantic
/// model (Cast, OfType), or a runtime helper of its own (Zip, GroupBy) — keeps its own strategy.
/// That is what a strategy is for; a table entry is for a shape.
/// </para>
/// </summary>
public class LinqTableStrategy : IExpressionIrStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (!invocation.TryGetInstanceCall(out _, out var name)) return false;
        if (Template(name.Identifier.Text, invocation.ArgumentList.Arguments.Count) is null) return false;

        // The SYMBOL decides when there is one; a NAME may decide only where the model cannot be
        // asked at all (CanGuess — the documented policy). Claiming by name FIRST and checking the
        // symbol afterwards is how a call gets refused after its receiver was already emitted.
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol method) return context.SemanticHelper.IsLinqExtension(method.ContainingType);
        return symbol is null && context.CanGuess(node);
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
        // Filtering, projection and quantifiers map one-for-one onto the array methods. These
        // arrived here from a strategy each, whose twenty lines of gate said what the gate above
        // now says once; the shape is what was actually theirs.
        ("Where", 1) => "{0}.filter({1})",
        ("Where", 0) => "{0}.filter(x => true)",
        ("Select", 1) => "{0}.map({1})",
        ("Select", 0) => "{0}.map(x => x)",
        ("Any", 1) => "{0}.some({1})",
        ("Any", 0) => "({0}.length > 0)",
        ("All", 1) => "{0}.every({1})",
        ("All", 0) => "{0}.every(x => true)",
        ("Concat", 1) => "[...{0}, ...{1}]",
        // Reverse is deliberately NOT here: `List<T>.Reverse()` is an INSTANCE method that
        // reverses in place and returns void, and only `Enumerable.Reverse()` returns a new
        // sequence. One name, two meanings decided by the receiver — a reason, not a shape.
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
