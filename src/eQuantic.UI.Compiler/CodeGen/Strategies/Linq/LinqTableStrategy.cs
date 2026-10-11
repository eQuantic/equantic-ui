using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Types;

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
        // The shape is the one of the arguments the template takes, a key comparer the fence passes
        // being dropped. A ToDictionary of three arguments no model binds has no shape of its own: its
        // third is a comparer, which nothing can judge without a model, and ConvertIr refuses it.
        var shaped = Shaped(name.Identifier.Text, invocation, context).Count;
        if (Template(name.Identifier.Text, shaped) is null
            && !IsToDictionaryWithAComparer(name.Identifier.Text, shaped)
            && name.Identifier.Text != "ToHashSet") return false;

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

        if (name.Identifier.Text == "ToHashSet") return ToHashSet(invocation, receiverSyntax, context);

        // A key comparer is the collection fence's to judge (#578): one that asks for what the shape
        // already does is dropped, and any other is refused, the call written as its own C# text, as
        // ToHashSet's is. ToDictionary refused every comparer, StringComparer.Ordinal included, and
        // ToLookup took one for an element selector and called it.
        foreach (var comparer in KeyComparers(name.Identifier.Text, invocation, context))
            if (comparer.RefusesAsUntranslatable(name.Identifier.Text, context)) return JsExpr.Opaque(invocation.ToString());
        var shaped = Shaped(name.Identifier.Text, invocation, context);

        var receiver = LinqSource.Ir(receiverSyntax, context);
        var args = shaped
            .Select(a => LinqSource.Argument(a, invocation, context))
            .ToArray();

        var template = Template(name.Identifier.Text, args.Length);
        if (name.Identifier.Text == "ToDictionary" && ToDictionary(invocation, args.Length, context) is var (dictionary, refusal))
        {
            if (refusal is not null) return JsExpr.Opaque(context.Unhandled(invocation, refusal));
            template = dictionary;
        }
        // Only a ToDictionary of three arguments no model binds reaches here without a shape, and its
        // third is a comparer: a call no model binds has nothing to judge it by but its count.
        if (template is null) return JsExpr.Opaque(context.Unhandled(invocation, "ToDictionary with a comparer"));
        // A lookup groups by the key type's equality, as GroupBy does: by === two equal records
        // were two groups.
        if (name.Identifier.Text == "ToLookup"
            && context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { TypeArguments: [_, var lookupKey, ..] })
        {
            template = template.Replace("$x.key === $key", LinqKeys.Matches(lookupKey, "$x.key", "$key"));
        }
        if (template.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);

        // {0} is the receiver; {1}… the arguments. The writer binds whatever is reused.
        return JsExpr.Template(BindNamedArguments(template, shaped, invocation, context),
            new[] { receiver }.Concat(args).ToArray());
    }

    /// <summary>The operators of the table that take a key COMPARER beside their selectors, whose
    /// overloads with one share their argument counts with the ones with an element selector.</summary>
    private static bool TakesAKeyComparer(string name) => name is "ToDictionary" or "ToLookup";

    /// <summary>The key comparers a call hands an operator that takes one, as the bound tree passes
    /// them: each argument that fills an <c>IEqualityComparer&lt;TKey&gt;</c>.</summary>
    private static IEnumerable<IOperation> KeyComparers(string name, InvocationExpressionSyntax invocation, ConversionContext context) =>
        TakesAKeyComparer(name) && context.SemanticHelper.GetOperation(invocation) is IInvocationOperation call
            ? call.Arguments
                .Where(argument => argument.ArgumentKind != ArgumentKind.DefaultValue && argument.Parameter?.Type.IsEqualityComparer() == true)
                .Select(argument => argument.Value)
            : [];

    /// <summary>
    /// The arguments the template takes, in the order they are written: every one but the key comparer
    /// of an operator that takes one, which the fence has passed (<see cref="KeyComparers"/>) and which
    /// asks for what the template already does. <c>ToDictionary(k, comparer)</c> is
    /// <c>ToDictionary(k)</c>'s shape, where its count made it the shape of <c>ToDictionary(k, e)</c>.
    /// The comparer is found by its POSITION among the arguments the bound tree names, so a call a
    /// strategy rebuilt (a <c>?.</c>'s, which copies its arguments position by position) drops its own.
    /// </summary>
    private static IReadOnlyList<ArgumentSyntax> Shaped(string name, InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (!TakesAKeyComparer(name) || context.SemanticHelper.GetOperation(invocation) is not IInvocationOperation call) return arguments;
        var written = (context.SemanticHelper.Original(invocation) as InvocationExpressionSyntax)?.ArgumentList.Arguments ?? arguments;
        var comparers = call.Arguments
            .Where(argument => argument.Parameter?.Type.IsEqualityComparer() == true && argument.Syntax is ArgumentSyntax)
            .Select(argument => written.IndexOf((ArgumentSyntax)argument.Syntax))
            .ToHashSet();
        return arguments.Where((_, at) => !comparers.Contains(at)).ToList();
    }

    /// <summary>
    /// The table's holes past <c>{0}</c> are the call's PARAMETERS in order, and the arguments arrive
    /// in the order they were WRITTEN: the same thing until one is named, when
    /// <c>Aggregate(func: f, seed: s)</c> reduced with the seed as the function and
    /// <c>ToDictionary(elementSelector: e, keySelector: k)</c> keyed by the element. Each parameter
    /// hole is pointed at the argument that fills it, and the arguments stay in the order C#
    /// evaluates them, which the template writer keeps. <paramref name="arguments"/> are the ones the
    /// template takes (<see cref="Shaped"/>), a dropped comparer, which is always the last parameter,
    /// filling no hole.
    /// </summary>
    private static string BindNamedArguments(string template, IReadOnlyList<ArgumentSyntax> arguments,
        InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (arguments.All(argument => argument.NameColon is null)) return template;
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol { MethodKind: MethodKind.ReducedExtension } method)
            return template;
        var writtenForSlot = Enumerable.Repeat(-1, method.Parameters.Length).ToArray();
        for (var i = 0; i < arguments.Count; i++)
        {
            var named = arguments[i].NameColon?.Name.Identifier.ValueText;
            var slot = named is null ? i : method.Parameters.FirstOrDefault(parameter => parameter.Name == named)?.Ordinal ?? -1;
            if (slot < 0 || slot >= writtenForSlot.Length) return template;
            writtenForSlot[slot] = i;
        }
        var holes = new System.Text.RegularExpressions.Regex(@"\{(\d+)\}");
        if (holes.Matches(template).Select(hole => int.Parse(hole.Groups[1].Value))
            .Any(hole => hole > 0 && (hole > writtenForSlot.Length || writtenForSlot[hole - 1] < 0)))
        {
            return template;
        }
        return holes.Replace(template, hole =>
        {
            var index = int.Parse(hole.Groups[1].Value);
            return index == 0 ? hole.Value : "{" + (writtenForSlot[index - 1] + 1) + "}";
        });
    }

    /// <summary>
    /// <c>ToDictionary</c> by the runtime, which refuses a null key and a key twice as .NET does, into
    /// the dictionary class a constructed one is, its keys found by value where the key type's default
    /// comparer finds them so (<see cref="ElementEquality"/>). A comparer reaches here only when it asks
    /// for that, the fence having refused any other, and is dropped (#578); an enum with aliases is
    /// refused, whose two names for one value are two keys on this side. <paramref name="selectors"/> is
    /// how many selectors the call passes. Null is a call no model binds, which keeps the table's shape.
    /// </summary>
    private static (string? Template, string? Refusal)? ToDictionary(InvocationExpressionSyntax invocation, int selectors,
        ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol { TypeArguments: [_, var key, ..] })
            return null;
        if ((key.UnwrapNullable() ?? key) is INamedTypeSymbol { TypeKind: TypeKind.Enum } keyEnum && LinqKeys.HasAliases(keyEnum))
            return (null, $"ToDictionary keyed by {key.ToDisplayString()}, an enum with aliases");
        var holes = selectors == 2 ? "{1}, {2}" : "{1}";
        var byValue = ElementEquality.Of(key) is { } equality
            ? selectors == 2 ? $", {equality}" : $", null, {equality}"
            : "";
        return ($"{Eq.LinqToDictionary}({{0}}, {holes}{byValue})", null);
    }

    private static bool IsToDictionaryWithAComparer(string name, int argCount) => name == "ToDictionary" && argCount == 3;

    /// <summary>
    /// <c>ToHashSet()</c>, which is <c>new HashSet&lt;T&gt;(source)</c>: the runtime's set, by the element
    /// type's equality, handed the source as it is, so a set is copied as .NET copies one, slots and
    /// all. A <c>Set</c> dropped a date, a decimal, a tuple and a record equal to one already there
    /// on the floor of identity (#531). A comparer the collection fence would let through, the
    /// default's, is the one the set has already; any other has no form here and is refused.
    /// </summary>
    private static JsExpr ToHashSet(InvocationExpressionSyntax invocation, ExpressionSyntax receiver, ConversionContext context)
    {
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (context.SemanticHelper.GetOperation(argument.Expression) is not { } comparer)
                return JsExpr.Opaque(context.Unhandled(invocation, "ToHashSet with a comparer"));
            // Refused and reported: the call's own C# text, as ConversionContext.Unhandled writes one, which
            // fails where it runs rather than standing in as a set nothing built.
            if (comparer.RefusesAsUntranslatable("ToHashSet", context)) return JsExpr.Opaque(invocation.ToString());
        }
        context.UsedHelpers.Add(Eq.Import);
        var element = method is { TypeArguments: [var item] } ? item : null;
        return JsExpr.Call(JsExpr.Identifier(Eq.HashSet), JsExpr.Literal(ElementEquality.Of(element) ?? "false"),
            context.Converter.ConvertIr(receiver));
    }

    private static string? Template(string name, int argCount) => (name, argCount) switch
    {
        // Filtering, projection and quantifiers map one-for-one onto the array methods. These
        // arrived here from a strategy each, whose twenty lines of gate said what the gate above
        // now says once; the shape is what was actually theirs.
        ("Where", 1) => "{0}.filter({1})",
        ("Where", 0) => "{0}.filter(($x) => true)",
        ("Select", 1) => "{0}.map({1})",
        ("Select", 0) => "{0}.map(($x) => $x)",
        ("Any", 1) => "{0}.some({1})",
        ("Any", 0) => "({0}.length > 0)",
        ("All", 1) => "{0}.every({1})",
        ("All", 0) => "{0}.every(($x) => true)",
        ("Concat", 1) => "[...{0}, ...{1}]",
        // Reverse is deliberately NOT here: `List<T>.Reverse()` is an INSTANCE method that
        // reverses in place and returns void, and only `Enumerable.Reverse()` returns a new
        // sequence. One name, two meanings decided by the receiver — a reason, not a shape.
        // Set operations: a Set dedupes the source, the other side is a membership test.
        ("Union", 1) => "[...new Set([...{0}, ...{1}])]",
        ("Intersect", 1) => "[...new Set({0})].filter(($x) => {1}.includes($x))",
        ("Except", 1) => "[...new Set({0})].filter(($x) => !{1}.includes($x))",
        // Folding and flattening. Aggregate's arguments swap: C# takes (seed, func), reduce (func, seed).
        ("Aggregate", 2) => "{0}.reduce({2}, {1})",
        ("Aggregate", 1) => "{0}.reduce({1})",
        ("SelectMany", 1) => "{0}.flatMap({1})",
        ("SelectMany", 0) => "{0}.flatMap(($x) => $x)",
        // A key wins by comparing the SELECTED value; reduce keeps the first of equals, as .NET does.
        ("MaxBy", 1) => "{0}.reduce(($a, $b) => (({1})($b) > ({1})($a) ? $b : $a))",
        ("MinBy", 1) => "{0}.reduce(($a, $b) => (({1})($b) < ({1})($a) ? $b : $a))",
        ("ToDictionary", 2) => $"{Eq.LinqToDictionary}({{0}}, {{1}}, {{2}})",
        ("ToDictionary", 1) => $"{Eq.LinqToDictionary}({{0}}, {{1}})",
        // Partitioning by predicate: neither has an array method, so each is a loop that stops.
        ("TakeWhile", 1) =>
            "(function($arr) { const $res = []; for (const $x of $arr) { if (({1})($x)) $res.push($x); else break; } return $res; })({0})",
        ("SkipWhile", 1) =>
            "(function($arr) { const $res = []; let $skipping = true; for (const $x of $arr) { if ($skipping && ({1})($x)) continue; $skipping = false; $res.push($x); } return $res; })({0})",
        ("DistinctBy", 1) =>
            "(($arr) => { const $seen = new Set(); return $arr.filter(($x) => { const $k = ({1})($x); if ($seen.has($k)) return false; $seen.add($k); return true; }); })({0})",
        ("Chunk", 1) =>
            "(($arr) => { const $n = {1}; const $out = []; for (let $i = 0; $i < $arr.length; $i += $n) $out.push($arr.slice($i, $i + $n)); return $out; })({0})",
        // A lookup is an array of arrays, each carrying its key — what a grouping is on this side.
        ("ToLookup", 2) =>
            "{0}.reduce(($groups, $item) => { const $key = ({1})($item); let $g = $groups.find(($x) => $x.key === $key); if (!$g) { $g = []; $g.key = $key; $groups.push($g); } $g.push(({2})($item)); return $groups; }, [])",
        ("ToLookup", 1) =>
            "{0}.reduce(($groups, $item) => { const $key = ({1})($item); let $g = $groups.find(($x) => $x.key === $key); if (!$g) { $g = []; $g.key = $key; $groups.push($g); } $g.push($item); return $groups; }, [])",
        // Both joins index the INNER sequence once and then walk the outer, which is the shape
        // that keeps a join linear instead of quadratic.
        ("Join", 4) =>
            "(() => { const $m = new Map(); for (const $x of {1}) { const $k = ({3})($x); let $g = $m.get($k); if (!$g) $m.set($k, $g = []); $g.push($x); } const $r = []; for (const $y of {0}) { const $g = $m.get(({2})($y)); if ($g) for (const $z of $g) $r.push(({4})($y, $z)); } return $r; })()",
        ("GroupJoin", 4) =>
            "(() => { const $m = new Map(); for (const $x of {1}) { const $k = ({3})($x); let $g = $m.get($k); if (!$g) $m.set($k, $g = []); $g.push($x); } return {0}.map(($y) => ({4})($y, $m.get(({2})($y)) ?? [])); })()",
        ("Append", 1) => "[...{0}, {1}]",
        ("Prepend", 1) => "[{1}, ...{0}]",
        ("AsEnumerable", 0) => "{0}",
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
