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
}
