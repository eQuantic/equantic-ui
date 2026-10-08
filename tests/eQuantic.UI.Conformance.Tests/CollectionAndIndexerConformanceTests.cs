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
    // A complex element initializer hands its one expression to Add.
    [InlineData("var a = new List<int> { { 1 }, 2 }; List<int> b = new() { { 3 } }; return string.Join(\",\", a) + \"|\" + (a[0] + 1) + \"|\" + b[0];")] // 1,2|2|3
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
    // After a null-conditional, whose call is rebuilt over its receiver, the comparer is still the one dropped.
    [InlineData("string[] none = null; var words = new[] { \"a\", \"bb\" }; var d = none?.ToDictionary(w => w, StringComparer.Ordinal); var e = words?.ToDictionary(w => w.Length, EqualityComparer<int>.Default); var l = words?.ToLookup(w => w.Length, EqualityComparer<int>.Default); return (d == null) + \"|\" + e[2] + \"|\" + l[1].Count();")] // True|bb|1
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

    /// <summary>
    /// A read or a call through a list's face, or a collection's, that holds null throws .NET's
    /// NullReferenceException in its words, and a null-conditional one answers null. The runtime's count
    /// counted a null as none, so <c>r.Count</c> answered 0 where .NET throws, and the element reads and
    /// the calls threw JavaScript's own TypeError (#586, #593).
    /// </summary>
    [SkippableTheory]
    [InlineData("IReadOnlyList<int> r = null; try { return r.Count.ToString(); } catch (NullReferenceException e) { return e.Message; }")]
    [InlineData("ICollection<int> c = null; try { return c.Count.ToString(); } catch (NullReferenceException e) { return e.Message; }")]
    [InlineData("IList<int> l = null; try { return l[^1].ToString(); } catch (NullReferenceException e) { return e.Message; }")]
    [InlineData("IReadOnlyList<int> r = null; try { return r[0].ToString(); } catch (NullReferenceException e) { return e.Message; }")]
    [InlineData("IList<int> l = null; try { l[0] = 1; return \"written\"; } catch (NullReferenceException e) { return e.Message; }")]
    [InlineData("ICollection<int> c = null; try { c.Add(1); return \"added\"; } catch (NullReferenceException e) { return e.Message; }")]
    [InlineData("ICollection<int> c = null; try { c.Clear(); return \"cleared\"; } catch (NullReferenceException e) { return e.Message; }")]
    // A null-conditional answers null, never 0, and counts what is there; a pattern tests for null first.
    [InlineData("IReadOnlyList<int> r = null; IReadOnlyList<int> s = new List<int> { 1, 2 }; var d = new Dictionary<string, int?> { [\"r\"] = r?.Count, [\"s\"] = s?.Count }; return d.ContainsValue(null) + \"|\" + d[\"s\"];")] // True|2
    [InlineData("ICollection<int> c = null; IReadOnlyList<int> r = null; return (c?.Count ?? -1) + \"|\" + (c?.Count > 0) + \"|\" + (r?[0] ?? -1) + \"|\" + (r is { Count: > 0 });")] // -1|False|-1|False
    public void AReadThroughANullFace_ThrowsAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// <c>ICollection&lt;T&gt;</c>'s <c>Add</c> and <c>Clear</c> answer for the collection the interface
    /// holds when the call runs, as <c>Contains</c>, <c>Remove</c> and <c>CopyTo</c> already did: a set
    /// adds what it does not hold, a linked list adds last, a dictionary adds the pair's key and refuses
    /// one already there. They were an array's <c>push</c> and <c>splice</c>, which none of those has
    /// (#593).
    /// </summary>
    [SkippableTheory]
    // The rows of #593.
    [InlineData("ICollection<int> c = new HashSet<int> { 1 }; c.Add(2); c.Add(1); return c.Count + \"|\" + string.Join(\",\", c);")]         // 2|1,2
    [InlineData("ICollection<int> c = new LinkedList<int>(new[] { 1 }); c.Add(2); return c.Count + \"|\" + string.Join(\",\", c);")]         // 2|1,2
    [InlineData("ICollection<int> c = new HashSet<int> { 1 }; c.Clear(); return c.Count;")]                                                // 0
    // The same two over a linked list, a sorted set and a dictionary's pairs.
    [InlineData("ICollection<int> c = new LinkedList<int>(new[] { 1, 2 }); c.Clear(); c.Add(3); return c.Count + \"|\" + string.Join(\",\", c);")] // 1|3
    [InlineData("ICollection<int> c = new SortedSet<int> { 3 }; c.Add(1); c.Add(3); return string.Join(\",\", c);")]                       // 1,3
    [InlineData("ICollection<int> c = new SortedSet<int> { 3 }; c.Clear(); return c.Count;")]                                               // 0
    [InlineData("ICollection<KeyValuePair<string, int>> c = new Dictionary<string, int>(); c.Add(new KeyValuePair<string, int>(\"a\", 1)); return c.Count + \"|\" + string.Join(\",\", c.Select(p => p.Key + p.Value));")] // 1|a1
    [InlineData("ICollection<KeyValuePair<string, int>> c = new Dictionary<string, int> { [\"a\"] = 1 }; try { c.Add(new KeyValuePair<string, int>(\"a\", 2)); return \"added\"; } catch (Exception e) { return e.Message; }")]
    [InlineData("ICollection<KeyValuePair<string, int>> c = new Dictionary<string, int> { [\"a\"] = 1 }; c.Clear(); return c.Count;")]       // 0
    [InlineData("ICollection<KeyValuePair<int, string>> c = new SortedDictionary<int, string>(); c.Add(new KeyValuePair<int, string>(2, \"b\")); c.Add(new KeyValuePair<int, string>(1, \"a\")); return string.Join(\",\", c.Select(p => p.Value));")] // a,b
    [InlineData("ICollection<KeyValuePair<int, string>> c = new SortedList<int, string> { [2] = \"b\" }; try { c.Add(new KeyValuePair<int, string>(2, \"c\")); return \"added\"; } catch (Exception e) { return e.Message; }")]
    // A list behind the interface as before, and a set behind ISet<T>, whose Clear is ICollection<T>'s.
    [InlineData("ICollection<int> c = new List<int> { 1 }; c.Add(2); c.Clear(); c.Add(3); return string.Join(\",\", c);")]                 // 3
    [InlineData("IList<int> l = new List<int> { 1 }; l.Add(2); return string.Join(\",\", l) + \"|\" + l.Count;")]                          // 1,2|2
    [InlineData("ISet<int> s = new HashSet<int> { 1, 2 }; s.Clear(); return s.Count;")]                                                     // 0
    public void ICollectionsAddAndClear_AnswerForTheCollectionBehindIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    private const string Bags = """
        using System.Collections;
        using System.Collections.Generic;

        public class Bag : ICollection<int>
        {
            private readonly List<int> _items = new();
            public int Adds;
            public int Count => _items.Count;
            public bool IsReadOnly => false;
            public void Add(int item) { Adds++; _items.Add(item); }
            public void Clear() => _items.Clear();
            public bool Contains(int item) => _items.Contains(item);
            public void CopyTo(int[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
            public bool Remove(int item) => _items.Remove(item);
            public IEnumerator<int> GetEnumerator() { foreach (var item in _items) yield return item; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class Holder
        {
            public ICollection<int> Items { get; } = new HashSet<int> { 1 };
            public ICollection<int> Bagged { get; } = new Bag();
        }
        """;

    private static readonly (string Name, string Statements)[] BagCases =
    [
        // "2|0|2"
        ("a class of the app's own behind the interface", "ICollection<int> c = new Bag(); c.Add(1); c.Add(2); var n = c.Count; c.Clear(); return n + \"|\" + c.Count + \"|\" + ((Bag)c).Adds;"),
        // "2|1": a member's collection initializer adds through the interface's Add
        ("a member's initializer adds through the interface", "var h = new Holder { Items = { 1, 2 }, Bagged = { 5 } }; return h.Items.Count + \"|\" + h.Bagged.Count;"),
    ];

    public static TheoryData<string, bool> BagCaseNames() => Each(BagCases);

    [SkippableTheory]
    [MemberData(nameof(BagCaseNames))]
    public void ICollectionsAddAndClear_ReachAClassOfTheAppsOwn(string name, bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Bags, typeAnnotations, BagCases.Single(c => c.Name == name));

    private const string Slices = """
        using System.Collections.Generic;

        public class Strip
        {
            public static string Text = "";
            private readonly int[] _v = { 1, 2, 3, 4, 5 };
            public int Length { get { Text += "L"; return _v.Length; } }
            public int this[int i] => _v[i];
            public int[] Slice(int start, int length)
            {
                Text += "S" + start + "," + length;
                var r = new int[length];
                for (var i = 0; i < length; i++) r[i] = _v[start + i];
                return r;
            }
            public static Strip R(Strip s) { Text += "R"; return s; }
            public static int At(string step, int value) { Text += step; return value; }
        }

        public class Words
        {
            private readonly List<string> _v;
            public Words(params string[] v) { _v = new List<string>(v); }
            public int Count => _v.Count;
            public string this[int i] => _v[i];
            public Words Slice(int start, int length) => new Words(_v.GetRange(start, length).ToArray());
            public override string ToString() => string.Join(",", _v);
        }
        """;

    private static readonly (string Name, string Statements)[] SliceCases =
    [
        // "2|2,3": Slice(1, 2), where JavaScript's slice(1, 3) is three elements
        ("the row of #585", "var part = new Strip()[1..3]; return part.Length + \"|\" + string.Join(\",\", part);"),
        // "3,4"
        ("a range from the end", "var p = new Strip()[^3..^1]; return string.Join(\",\", p);"),
        // "1,2|4,5|1,2,3,4,5"
        ("an open start, an open end and both", "var s = new Strip(); return string.Join(\",\", s[..2]) + \"|\" + string.Join(\",\", s[3..]) + \"|\" + string.Join(\",\", s[..]);"),
        // "3,4,5|0"
        ("an end from the end at zero", "var s = new Strip(); return string.Join(\",\", s[2..^0]) + \"|\" + s[^0..].Length;"),
        // "2,3,4|2,3,4"
        ("named endpoints", "int a = 1, b = 4; var s = new Strip(); return string.Join(\",\", s[a..b]) + \"|\" + string.Join(\",\", s[^b..^a]);"),
        // the receiver once, then the endpoints in their order, then the length and the slice, as .NET reads them
        ("the order a range is read in", "Strip.Text = \"\"; var s = new Strip(); var p = Strip.R(s)[Strip.At(\"A\", 1)..Strip.At(\"B\", 3)]; return Strip.Text + \"|\" + p.Length;"),
        ("the order a range from the end is read in", "Strip.Text = \"\"; var s = new Strip(); var p = Strip.R(s)[^Strip.At(\"A\", 3)..^Strip.At(\"B\", 1)]; return Strip.Text + \"|\" + p.Length;"),
        ("the order an open range is read in", "Strip.Text = \"\"; var s = new Strip(); var p = Strip.R(s)[Strip.At(\"A\", 1)..]; return Strip.Text + \"|\" + p.Length;"),
        // "threw": Slice(3, -2) refuses a negative length, where slice(3, 1) answered one element
        ("a range that ends before it starts", "try { var p = new Strip()[3..1]; return \"sliced \" + p.Length; } catch (System.Exception) { return \"threw\"; }"),
        // "b,c|c,d": a type that counts and slices into itself
        ("a type that counts and slices into itself", "return new Words(\"a\", \"b\", \"c\", \"d\")[1..^1].ToString() + \"|\" + new Words(\"a\", \"b\", \"c\", \"d\")[1..][1..].ToString();"),
        // "2,3|2,3|bc|bc": a string, an array and a list keep JavaScript's slice, which answers alike there
        ("a string, an array and a list keep their slice", "var arr = new[] { 1, 2, 3, 4 }; var l = new List<int> { 1, 2, 3, 4 }; return string.Join(\",\", arr[1..3]) + \"|\" + string.Join(\",\", l[1..3]) + \"|\" + \"abcd\"[1..3] + \"|\" + \"abcd\"[^3..^1];"),
    ];

    public static TheoryData<string, bool> SliceCaseNames() => Each(SliceCases);

    /// <summary>
    /// A range over a type eqc writes, with a <c>Length</c> or a <c>Count</c> and a
    /// <c>Slice(int start, int length)</c>, calls that <c>Slice</c> as C# lowers the range: the receiver
    /// once, the start and the LENGTH computed from the endpoints and the count the bound tree names. It
    /// called the twin's <c>slice</c> with the range's end where <c>Slice</c> takes a length (#585).
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(SliceCaseNames))]
    public void ARangeOverATypeWithSlice_CallsItsSliceWithALength(string name, bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Slices, typeAnnotations, SliceCases.Single(c => c.Name == name));

    private const string Lists = """
        using System.Collections;
        using System.Collections.Generic;

        public class Ring : IReadOnlyList<int>
        {
            private readonly int[] _v = { 7, 8, 9 };
            public int Count => _v.Length;
            public int this[int i] => _v[i];
            public IEnumerator<int> GetEnumerator() { foreach (var v in _v) yield return v; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class Cells : IList<int>
        {
            public static string Text = "";
            private readonly List<int> _v = new() { 1, 2, 3 };
            public int Count => _v.Count;
            public bool IsReadOnly => false;
            public int this[int i] { get { Text += "g" + i; return _v[i]; } set { Text += "s" + i; _v[i] = value; } }
            public int IndexOf(int item) => _v.IndexOf(item);
            public void Insert(int index, int item) => _v.Insert(index, item);
            public void RemoveAt(int index) => _v.RemoveAt(index);
            public void Add(int item) => _v.Add(item);
            public void Clear() => _v.Clear();
            public bool Contains(int item) => _v.Contains(item);
            public void CopyTo(int[] array, int arrayIndex) => _v.CopyTo(array, arrayIndex);
            public bool Remove(int item) => _v.Remove(item);
            public IEnumerator<int> GetEnumerator() { foreach (var v in _v) yield return v; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public static IList<int> R(IList<int> l) { Text += "R"; return l; }
            public static int At(string step, int value) { Text += step; return value; }
        }

        public class Polyline : IReadOnlyList<int>
        {
            private readonly int[] _v = { 3, 4 };
            public double Length => 12.5;
            public int Size => 7;
            public int Count => _v.Length;
            public int this[int i] => _v[i];
            public IEnumerator<int> GetEnumerator() { foreach (var v in _v) yield return v; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class Shelf
        {
            public IList<int> L { get; } = new Cells();
        }

        public static class Sums
        {
            public static int Of(IReadOnlyList<int> xs) { var s = 0; for (var i = 0; i < xs.Count; i++) s += xs[i]; return s; }
        }
        """;

    private static readonly (string Name, string Statements)[] ListCases =
    [
        // 19: the twin's item and count, where a subscript and a length answered undefined
        ("the row of #586", "IReadOnlyList<int> r = new Ring(); return r[0] + r[2] + r.Count;"),
        // 97
        ("a read from the end", "IReadOnlyList<int> r = new Ring(); return r[^1] * 10 + r[^3];"),
        // 30: a twin, an array and a list through one parameter
        ("a parameter typed as the interface", "return Sums.Of(new Ring()) + Sums.Of(new[] { 1, 2 }) + Sums.Of(new List<int> { 3 });"),
        // "2|4|7": the face counts by the twin's own Count, never by a Length or a Size beside it
        ("a type of the app's own with a Length and a Size beside its count", "IReadOnlyList<int> p = new Polyline(); return p.Count + \"|\" + p[^1] + \"|\" + Sums.Of(p);"),
        // "5|4|4"
        ("a read, a write, a compound and a step through IList<T>", "IList<int> l = new Cells(); l[0] = 5; l[1] += 2; l[2]++; return l[0] + \"|\" + l[1] + \"|\" + l[2];"),
        // "7|7"
        ("a write answers the value written", "IList<int> l = new Cells(); var y = (l[0] = 7); return y + \"|\" + l[0];"),
        // "3|6"
        ("a write from the end", "IList<int> l = new Cells(); l[^1] = 6; l[^2] += 1; return l[1] + \"|\" + l[2];"),
        // "RKVs0": the receiver, the key, the value, then the setter
        ("a write's order", "Cells.Text = \"\"; IList<int> l = new Cells(); Cells.R(l)[Cells.At(\"K\", 0)] = Cells.At(\"V\", 4); return Cells.Text;"),
        // "RKg1Vs1|4"
        ("a compound's order", "Cells.Text = \"\"; IList<int> l = new Cells(); Cells.R(l)[Cells.At(\"K\", 1)] += Cells.At(\"V\", 2); return Cells.Text + \"|\" + l[1];"),
        // "8|-1"
        ("a null-conditional read", "IReadOnlyList<int> r = new Ring(); IReadOnlyList<int> none = null; return (r?[1] ?? -1) + \"|\" + (none?[1] ?? -1);"),
        // 9: an object initializer's entry written through the twin's setter, read back through its own indexer
        ("an object initializer's entry", "var s = new Shelf { L = { [0] = 9 } }; return ((Cells)s.L)[0];"),
        // 19: an array and a list behind the interfaces keep their subscript
        ("an array and a list behind the interfaces", "IList<int> l = new List<int> { 1, 2 }; l[0] = 5; l[1] += 2; IReadOnlyList<int> a = new[] { 1, 2, 3 }; return l[0] + l[1] + l[^1] + a[^1] + a.Count;"),
    ];

    public static TheoryData<string, bool> ListCaseNames() => Each(ListCases);

    /// <summary>
    /// An access through the indexer of a BCL interface (<c>IReadOnlyList&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>)
    /// reaches a twin's <c>item</c> and <c>setItem</c> (#427), and an array's subscript, whichever the
    /// interface holds when it runs, its count alike. It was a subscript, which a twin does not answer:
    /// <c>r[0] + r[2] + r.Count</c> over a twin was null where .NET says 19 (#586).
    /// </summary>
    [SkippableTheory]
    [MemberData(nameof(ListCaseNames))]
    public void AnIndexerOfABclInterface_ReachesTheTwinBehindIt(string name, bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Lists, typeAnnotations, ListCases.Single(c => c.Name == name));

    /// <summary>Each case by its name, in the SDK's TypeScript and in plain JavaScript.</summary>
    private static TheoryData<string, bool> Each(IEnumerable<(string Name, string Statements)> cases)
    {
        var data = new TheoryData<string, bool>();
        foreach (var (name, _) in cases)
        {
            data.Add(name, true);
            data.Add(name, false);
        }
        return data;
    }
}
