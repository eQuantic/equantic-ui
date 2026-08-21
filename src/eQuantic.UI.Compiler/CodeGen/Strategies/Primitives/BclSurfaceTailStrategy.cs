using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// The INSTANCE-member tail of the audited BCL surface: <c>Equals</c> on the exactly-comparable
/// primitives, the string members the dedicated strategy never grew, and the Dictionary/List
/// quality-of-life members (capacity hints are honest no-ops — the same policy that drops a
/// collection expression's <c>with(capacity:)</c>). Symbol-first, table-driven, single-evaluation
/// only: receiver-reusing shapes go through an arrow. Deliberately absent, and therefore still
/// visible in the audit baseline: <c>double.Equals</c> (NaN.Equals(NaN) is true — <c>===</c>
/// would lie), <c>decimal.Equals</c> (Decimal objects), value-keyed dictionaries (they lower to
/// $eq.collections.valueMap, not a plain object).
/// </summary>
public class BclSurfaceTailStrategy : IConversionStrategy
{
    public int Priority => 12;

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (!invocation.TryGetInstanceCall(out _, out var name)) return false;

        return context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { IsStatic: false } method
            && Template(method, name.Identifier.Text, invocation.ArgumentList.Arguments.Count) is not null;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        invocation.TryGetInstanceCall(out var receiverSyntax, out var name);

        var method = (IMethodSymbol)context.SemanticHelper.GetSymbol(invocation)!;
        var receiver = context.Converter.ConvertExpression(receiverSyntax);
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToArray();

        var template = Template(method, name.Identifier.Text, args.Length)!;
        return TemplateFill.With(template, new[] { receiver }.Concat(args).ToArray());
    }

    /// <summary>{0} = receiver, {1}… = arguments; null = not ours, stays fenced.</summary>
    private static string? Template(IMethodSymbol method, string name, int argCount)
    {
        var home = method.ContainingType;

        // Equals on the primitives whose JS representation compares EXACTLY with === : booleans,
        // chars (one-char strings), int32 (number) and int64 (BigInt — 3n === 3 is false, which is
        // also C#'s answer for a boxed cross-type compare). Guids ride as strings.
        if (name is "Equals" && argCount == 1
            && (home.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char
                    or SpecialType.System_Int32 or SpecialType.System_Int64
                || home is { Name: "Guid", ContainingNamespace.Name: "System" }))
        {
            return "({0} === {1})";
        }

        // Dispose through the INTERFACE — the runtime's disposable contract is a `dispose()`
        // method on every twin that hands one out (subscriptions, tickers), and the `using`
        // statement already lowers to the same call. Bound to a CONCRETE type, Dispose translates
        // through the normal member path instead.
        if (name is "Dispose" && argCount == 0
            && home.ToDisplayString() is "System.IDisposable" or "System.IAsyncDisposable")
        {
            return "{0}.dispose()";
        }

        if (home.SpecialType == SpecialType.System_String)
        {
            return (name, argCount) switch
            {
                ("Clone", 0) => "{0}",
                ("Normalize", 0) => "{0}.normalize()",
                ("IsNormalized", 0) => "(($s) => $s === $s.normalize())({0})",
                // Every .NET-recognized line ending becomes the eqc world's NewLine, '\n'.
                ("ReplaceLineEndings", 0) => "{0}.replace(/\\r\\n|[\\r\\n\\u0085\\f\\u2028\\u2029]/g, '\\n')",
                ("ReplaceLineEndings", 1) => "(($s, $r) => $s.replace(/\\r\\n|[\\r\\n\\u0085\\f\\u2028\\u2029]/g, $r))({0}, {1})",
                ("IndexOfAny", 1) =>
                    "(($s, $c) => { for (let $i = 0; $i < $s.length; $i++) if ($c.includes($s[$i])) return $i; return -1; })({0}, {1})",
                ("IndexOfAny", 2) =>
                    "(($s, $c, $n) => { for (let $i = $n; $i < $s.length; $i++) if ($c.includes($s[$i])) return $i; return -1; })({0}, {1}, {2})",
                ("LastIndexOfAny", 1) =>
                    "(($s, $c) => { for (let $i = $s.length - 1; $i >= 0; $i--) if ($c.includes($s[$i])) return $i; return -1; })({0}, {1})",
                _ => null,
            };
        }

        var definition = home.OriginalDefinition.ToDisplayString();

        if (definition == "System.Collections.Generic.Dictionary<TKey, TValue>"
            && home.IsDictionaryLike(out _))
        {
            return (name, argCount) switch
            {
                ("TryAdd", 2) =>
                    "(($d, $k, $v) => Object.prototype.hasOwnProperty.call($d, $k) ? false : ($d[$k] = $v, true))({0}, {1}, {2})",
                ("ContainsValue", 1) => "Object.values({0}).includes({1})",
                // Capacity hints have no JS meaning; EnsureCapacity ANSWERS a capacity, so the
                // requested one is the honest value.
                ("EnsureCapacity", 1) => "{1}",
                ("TrimExcess", 0) => "void 0",
                ("TrimExcess", 1) => "void 0",
                _ => null,
            };
        }

        if (definition == "System.Collections.Generic.List<T>")
        {
            return (name, argCount) switch
            {
                ("AsReadOnly", 0) => "{0}",
                ("Slice", 2) => "(($a, $s, $n) => $a.slice($s, $s + $n))({0}, {1}, {2})",
                ("EnsureCapacity", 1) => "{1}",
                ("TrimExcess", 0) => "void 0",
                _ => null,
            };
        }

        return null;
    }
}
