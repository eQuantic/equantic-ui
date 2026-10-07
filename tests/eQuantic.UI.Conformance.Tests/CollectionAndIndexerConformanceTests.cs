using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A collection built, read, sliced and added to as .NET's is, on both sides, whatever stands behind
/// the face the code reaches it through: a list built with an argument and an initializer (#564), a
/// LINQ operator handed a comparer the collection fence passes (#578), a range over a type with a
/// <c>Slice</c> (#585), an indexer of a BCL interface over a twin (#586), and <c>ICollection&lt;T&gt;</c>'s
/// <c>Add</c> and <c>Clear</c> over a set, a linked list or a dictionary's pairs (#593).
/// <para>
/// A case that needs a type of its own runs through the module graph an app's build writes, one case
/// at a time and in both the SDK's TypeScript and plain JavaScript, so a failure names its case.
/// </para>
/// </summary>
public class CollectionAndIndexerConformanceTests
{
    /// <summary>
    /// A list built with a constructor argument and a collection initializer holds both, as C# builds it:
    /// the source copied (a capacity ignored), then each element added in order. The capacity's form
    /// dropped the elements, <c>[]</c>, and the source's joined the copy and the elements with a comma,
    /// <c>source, [3]</c>, which in a declaration is a second declarator and in an argument a second
    /// argument; the target-typed form kept the elements and dropped the source (#564).
    /// </summary>
    [SkippableTheory]
    // The rows of #564.
    [InlineData("var a = new List<int>(10) { 1, 2 }; return string.Join(\",\", a) + \"|\" + a.Count;")]                                       // 1,2|2
    [InlineData("var source = new List<int> { 1 }; var b = new List<int>(source) { 3 }; b.Add(4); return string.Join(\",\", b) + \"|\" + string.Join(\",\", source);")] // 1,3,4|1
    // Target-typed, the same two.
    [InlineData("List<int> a = new(10) { 1, 2 }; return string.Join(\",\", a);")]                                                             // 1,2
    [InlineData("var source = new List<int> { 1, 2 }; List<int> c = new(source) { 3 }; c.Add(4); return string.Join(\",\", c) + \"|\" + source.Count;")] // 1,2,3,4|2
    // The target-typed copy with no element, which was an empty list, and an empty initializer after a source.
    [InlineData("var source = new List<int> { 1 }; List<int> c = new(source); c.Add(9); return string.Join(\",\", c) + \"|\" + source.Count;")]      // 1,9|1
    [InlineData("var source = new List<int> { 1 }; var c = new List<int>(source) { }; c.Add(9); return string.Join(\",\", c) + \"|\" + source.Count;")] // 1,9|1
    // In an argument, where the copy and the elements were two arguments, and with a named capacity.
    [InlineData("return string.Join(\",\", new List<int>(new[] { 1 }) { 2 });")]                                                              // 1,2
    [InlineData("return new List<string>(capacity: 4) { \"a\" }.Count;")]                                                                     // 1
    // A source of any shape: a set, a dictionary's pairs, a string's chars.
    [InlineData("var l = new List<int>(new HashSet<int> { 7, 8 }) { 9 }; return string.Join(\",\", l);")]                                    // 7,8,9
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var l = new List<KeyValuePair<string, int>>(d) { new(\"b\", 2) }; return string.Join(\",\", l.Select(p => p.Key + p.Value));")] // a1,b2
    [InlineData("var l = new List<char>(\"ab\") { 'c' }; return new string(l.ToArray());")]                                                   // abc
    // The source is read first, then each element in its order.
    [InlineData("var log = \"\"; List<int> Src() { log += \"s\"; return new List<int> { 0 }; } int At(string s, int v) { log += s; return v; } var l = new List<int>(Src()) { At(\"a\", 1), At(\"b\", 2) }; return log + \"|\" + string.Join(\",\", l);")] // sab|0,1,2
    public void AListBuiltWithAnArgumentAndAnInitializer_HoldsBoth(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A comparer handed to a LINQ operator that builds a keyed result passes the collection fence's own
    /// test: one that asks for the default (the key type's own, ordinal strings, null) answers as the
    /// operator does without it. <c>ToDictionary</c> refused every comparer, <c>StringComparer.Ordinal</c>
    /// included (EQ1004), <c>GroupBy</c> refused every one too (EQ2008), <c>ToLookup</c> took a comparer
    /// for an element selector and called it, or had no form at all after one, and <c>Distinct</c> dropped
    /// it, whatever it asked for (#578).
    /// </summary>
    [SkippableTheory]
    // The row of #578, and the comparers the fence passes: the default's, ordinal strings, and null.
    [InlineData("var d = new[] { \"a\", \"bb\" }.ToDictionary(w => w, StringComparer.Ordinal); return d[\"bb\"] + \"|\" + d.Count;")]          // bb|2
    [InlineData("var d = new[] { \"a\", \"bb\" }.ToDictionary(w => w, w => w.Length, StringComparer.Ordinal); return d[\"bb\"] + d[\"a\"];")]   // 3
    [InlineData("var d = new[] { \"a\", \"bb\" }.ToDictionary(w => w.Length, EqualityComparer<int>.Default); return d[2];")]                  // bb
    [InlineData("var d = new[] { \"a\", \"bb\" }.ToDictionary(w => w, (IEqualityComparer<string>)null); return d.Count;")]                    // 2
    // Named and out of order: each selector in its parameter, the comparer dropped.
    [InlineData("var d = new[] { \"a\", \"bb\" }.ToDictionary(comparer: StringComparer.Ordinal, keySelector: w => w + \"!\"); return d[\"bb!\"];")] // bb
    [InlineData("var d = new[] { \"a\", \"bb\" }.ToDictionary(comparer: EqualityComparer<int>.Default, elementSelector: w => w + \"?\", keySelector: w => w.Length); return d[2];")] // bb?
    // A key twice is refused as it is without a comparer.
    [InlineData("try { new[] { \"a\", \"a\" }.ToDictionary(w => w, StringComparer.Ordinal); return \"built\"; } catch (Exception e) { return e.Message; }")]
    // ToLookup takes a comparer where it takes an element selector, and after one.
    [InlineData("var l = new[] { \"a\", \"bb\", \"cc\" }.ToLookup(w => w.Length, EqualityComparer<int>.Default); return string.Join(\",\", l[2]) + \"|\" + l.Count;")] // bb,cc|2
    [InlineData("var l = new[] { \"a\", \"A\", \"a\" }.ToLookup(w => w, StringComparer.Ordinal); return l.Count + \"|\" + l[\"a\"].Count();")] // 2|2
    [InlineData("var l = new[] { \"a\", \"bb\", \"cc\" }.ToLookup(w => w.Length, w => w.ToUpper(), EqualityComparer<int>.Default); return string.Join(\",\", l[2]);")] // BB,CC
    // Distinct, GroupBy in its four shapes, and ToHashSet alike.
    [InlineData("return new[] { \"a\", \"A\", \"a\" }.Distinct(StringComparer.Ordinal).Count();")]                                          // 2
    [InlineData("return string.Join(\",\", new[] { 3, 1, 3 }.Distinct(EqualityComparer<int>.Default));")]                                   // 3,1
    [InlineData("return string.Join(\",\", new[] { \"a\", \"A\", \"a\" }.GroupBy(w => w, StringComparer.Ordinal).Select(g => g.Key + g.Count()));")] // a2,A1
    [InlineData("return string.Join(\",\", new[] { \"a\", \"bb\", \"cc\" }.GroupBy(w => w.Length, w => w.ToUpper(), EqualityComparer<int>.Default).Select(g => string.Join(\"\", g)));")] // A,BBCC
    [InlineData("return string.Join(\",\", new[] { \"a\", \"bb\", \"cc\" }.GroupBy(w => w.Length, (k, g) => k + \":\" + g.Count(), EqualityComparer<int>.Default));")] // 1:1,2:2
    [InlineData("return string.Join(\",\", new[] { \"a\", \"bb\", \"cc\" }.GroupBy(w => w.Length, w => w[0], (k, g) => k + new string(g.ToArray()), EqualityComparer<int>.Default));")] // 1a,2bc
    [InlineData("return new[] { \"a\", \"A\", \"a\" }.ToHashSet(StringComparer.Ordinal).Count;")]                                           // 2
    public void ALinqOperatorHandedTheDefaultsComparer_AnswersAsItDoesWithoutOne(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
