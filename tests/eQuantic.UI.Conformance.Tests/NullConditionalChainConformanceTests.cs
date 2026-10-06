using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Null-conditional access over the translated BCL surface — the guarded shape now goes through
/// the SAME strategies as the plain one (ConditionalAccessStrategy rebuilds the tail on a receiver
/// placeholder and maps the rebuilt nodes to their originals), so <c>s?.ToUpper()</c> is
/// <c>toUpperCase</c>, <c>list?.Where(p)</c> is <c>filter</c>, <c>list?.Count</c> is
/// <c>length</c>. Every case runs with a non-null AND a null receiver: the translation must
/// agree with .NET on the value when present and on null when absent.
/// </summary>
public class NullConditionalChainConformanceTests
{
    [SkippableTheory]
    // Strings
    [InlineData("string? s = \"ab\"; return s?.ToUpper();")]                                  // "AB"
    [InlineData("string? s = null; return s?.ToUpper();")]                                    // null
    [InlineData("string? s = \" ab \"; return s?.Trim().ToUpper();")]                         // "AB" — a chain behind the guard
    [InlineData("string? s = null; return s?.Trim().ToUpper();")]                             // null
    [InlineData("string? s = \"abc\"; return s?.Length;")]                                    // 3
    [InlineData("string? s = null; return s?.Length;")]                                       // null
    [InlineData("string? s = \"banana\"; return s?.Substring(1, 3);")]                        // "ana"
    [InlineData("string? s = \"banana\"; return s?.Contains(\"nan\");")]                      // true
    [InlineData("string? s = \"a,b\"; return s?.Split(',').Length;")]                         // 2
    // Collections
    [InlineData("List<int>? l = new() { 1, 2, 3 }; return l?.Where(x => x > 1).Count();")]    // 2
    [InlineData("List<int>? l = null; return l?.Where(x => x > 1).Count();")]                 // null
    [InlineData("List<int>? l = new() { 1, 2, 3 }; return l?.Count;")]                        // 3 — the property, not a rename
    [InlineData("List<int>? l = null; return l?.Count;")]                                     // null
    [InlineData("List<int>? l = new() { 5, 6 }; return l?.FirstOrDefault();")]                // 5
    [InlineData("List<int>? l = new() { 5, 6 }; return l?.Any();")]                           // true
    [InlineData("List<int>? l = null; return l?.Any();")]                                     // null
    [InlineData("List<int>? l = new() { 1, 2, 3 }; return l?.Select(x => x * 2).Sum();")]     // 12
    [InlineData("List<int>? l = new() { 1, 2, 3 }; return l?.Contains(2);")]                  // true
    [InlineData("List<int>? l = new() { 7, 8 }; return l?[1];")]                              // 8
    [InlineData("List<int>? l = null; return l?[1];")]                                        // null
    // Dictionaries
    [InlineData("Dictionary<string, int>? d = new() { [\"a\"] = 1 }; return d?.ContainsKey(\"a\");")]   // true
    [InlineData("Dictionary<string, int>? d = null; return d?.ContainsKey(\"a\");")]          // null
    [InlineData("Dictionary<string, int>? d = new() { [\"a\"] = 1 }; return d?.Count;")]      // 1
    // Nested guards
    [InlineData("string? s = \"xy\"; return s?.ToUpper()?.ToLower();")]                       // "xy"
    [InlineData("string? s = null; return s?.ToUpper()?.ToLower();")]                         // null
    public void NullConditionalChains_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A tail whose translation is a helper, behind a receiver that is NOT a local: a call, an
    /// element, a property, a guard in the tail of another. The receiver is assigned to a temporary
    /// the statement around it declares (#539), so it is evaluated once, and each place a statement
    /// can stand declares its own: a body written without braces, a loop, a loop a label names, two
    /// sections of one switch, and a concise lambda, which declares it in a block of its own.
    /// </summary>
    [SkippableTheory]
    [InlineData("var gets = 0; string? Get() { gets++; return \" ab \"; } var r = Get()?.Trim(); return r + gets;")]                      // ab1
    [InlineData("var gets = 0; string? Get() { gets++; return null; } var r = Get()?.Trim(); return (r ?? \"null\") + gets;")]           // null1
    [InlineData("var xs = new[] { \" a \", null }; return (xs[0]?.Trim() ?? \"null\") + (xs[1]?.Trim() ?? \"null\");")]                 // anull
    [InlineData("var o = new { Name = \" ab \" }; return o.Name?.Trim();")]                                                               // ab
    [InlineData("string? Get() => \"abc\"; string? Other() => \" b \"; return Get()?.Replace(Other()?.Trim() ?? \"q\", \"x\");")]       // axc
    [InlineData("Func<string?[], int, string?> f = (a, i) => a[i]?.Trim(); "
        + "return (f(new[] { \" x \" }, 0) ?? \"null\") + (f(new string?[] { null }, 0) ?? \"null\");")]                                  // xnull
    [InlineData("string? Get() => \" y \"; string? x = null; var flag = true; if (flag) x = Get()?.Trim(); return x;")]                   // y
    [InlineData("var xs = new[] { \" a \", null, \" c \" }; var s = \"\"; for (var i = 0; i < xs.Length; i++) s += xs[i]?.Trim() ?? \"-\"; return s;")] // a-c
    [InlineData("var items = new[] { \" a \", \" b \", null }; var at = -1; string? Next() { at++; return at < items.Length ? items[at] : null; } "
        + "var seen = \"\"; outer: while (Next()?.Trim() is { } v) { if (v == \"b\") continue outer; seen += v; } return seen + at;")]    // a2
    [InlineData("var k = 2; string? Get() => \" z \"; string? r = null; "
        + "switch (k) { case 1: r = Get()?.Trim() + \"1\"; break; case 2: r = Get()?.Trim() + \"2\"; break; } return r;")]               // z2
    public void BehindAReceiverThatIsNotALocal_TheTailRunsOnceAndOnlyWhenItShould(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
