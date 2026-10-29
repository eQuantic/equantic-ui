using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>List&lt;T&gt;.AddRange</c> and <c>InsertRange</c> answer as .NET's do, for a range of any size (#1060).
/// They were <c>push(...items)</c> and <c>splice(index, 0, ...items)</c>. A spread hands every element to
/// the call as an ARGUMENT, and an engine caps a call's arguments by its stack: measured with a spread
/// into <c>push</c>, near 638,000 in JavaScriptCore (Safari's engine, and the Bun the SDK embeds) and
/// near 124,000 in V8, so a million elements threw a RangeError in every browser, and nothing said so
/// at build time. A spread into an array literal has no such cap, so <c>Concat</c>, <c>ToList</c>,
/// <c>new List&lt;T&gt;(items)</c> and a collection expression's <c>..items</c> took a million as
/// they are, and so did <c>RemoveAll</c>'s single pass and <c>Max</c>'s and <c>Min</c>'s loops.
/// </summary>
public class ListRangeConformanceTests
{
    /// <summary>A million ints, 0 to 999,999: past every engine's cap on a call's arguments.</summary>
    private const string Million = "var items = new int[1_000_000]; for (var i = 0; i < items.Length; i++) items[i] = i; ";

    /// <summary>A view from 2 to 6 of a set of 1, 3, 5 and 7, its count never read.</summary>
    private const string View = "var t = new SortedSet<int> { 1, 3, 5, 7 }; var v = t.GetViewBetween(2, 6); ";

    [SkippableTheory]
    // An array, a list and a sequence the runtime enumerates, each a RangeError in the browser.
    [InlineData(Million + "var list = new List<int> { -1 }; list.AddRange(items); return list.Count + \"|\" + list[0] + \"|\" + list[1_000_000];")] // "1000001|-1|999999"
    [InlineData(Million + "var source = new List<int>(items); var list = new List<int>(); list.AddRange(source); return list.Count + \"|\" + list[999_999];")] // "1000000|999999"
    [InlineData("var list = new List<int>(); list.AddRange(Enumerable.Range(0, 1_000_000)); return list.Count + \"|\" + list[999_999];")] // "1000000|999999"
    [InlineData(Million + "var list = new List<int> { -1, -2 }; list.InsertRange(1, items); return list.Count + \"|\" + list[0] + \"|\" + list[1] + \"|\" + list[1_000_000] + \"|\" + list[1_000_001];")] // "1000002|-1|0|999999|-2"
    public void ARangeOfAnySize_IsAddedAndInserted(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// What else the spread got wrong. A null collection is refused by its name, where the spread threw a
    /// TypeError; a string adds its UTF-16 chars, where the spread read its code points; an index past the
    /// end or below zero is refused, where <c>splice</c> appended or counted from the end; the collection
    /// is checked before the index, as .NET checks them; a named argument fills the parameter it names,
    /// where the collection took the index's place and was spread from a number; and a collection is
    /// read as one, as .NET reads it.
    /// </summary>
    [SkippableTheory]
    [InlineData("try { new List<int>().AddRange(null); return \"added\"; } catch (ArgumentNullException e) { return e.Message; }")] // "Value cannot be null. (Parameter 'collection')"
    [InlineData("try { new List<int>().InsertRange(0, null); return \"inserted\"; } catch (ArgumentNullException e) { return e.Message; }")] // "Value cannot be null. (Parameter 'collection')"
    [InlineData("try { new List<int>().InsertRange(5, null); return \"inserted\"; } catch (ArgumentNullException) { return \"collection\"; } catch (ArgumentOutOfRangeException) { return \"index\"; }")] // "collection"
    [InlineData("var l = new List<char>(); l.AddRange(\"a\\uD83D\\uDE00\"); return l.Count;")] // 3
    [InlineData("var l = new List<char> { 'x' }; l.InsertRange(0, \"a\\uD83D\\uDE00\"); return l.Count;")] // 4
    [InlineData("try { var l = new List<int> { 1 }; l.InsertRange(2, new[] { 9 }); return \"inserted \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // "Index was out of range. …"
    [InlineData("try { var l = new List<int> { 1 }; l.InsertRange(-1, new[] { 9 }); return \"inserted \" + string.Join(\",\", l); } catch (ArgumentOutOfRangeException e) { return e.Message; }")] // "Index was out of range. …"
    [InlineData("var order = \"\"; var list = new List<int> { 0 }; List<int> L() { order += \"l\"; return list; } int I() { order += \"i\"; return 1; } int[] C() { order += \"c\"; return new[] { 9 }; } L().InsertRange(collection: C(), index: I()); return order + string.Join(\",\", list);")] // "lci0,9"
    // A sorted set's view is read as a collection, its count taken again as .NET's AddRange takes it,
    // where the spread only walked it: an ExceptWith after it reads that count.
    [InlineData(View + "new List<int>().AddRange(v); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")] // "1,5,7"
    [InlineData(View + "new List<int> { 0 }.InsertRange(1, v); v.ExceptWith(new[] { 3 }); return string.Join(\",\", t);")] // "1,5,7"
    public void ARange_IsCheckedAndPlacedAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// What the spread already answered as .NET does, which a loop over the range could break: a list
    /// added to or inserted into itself takes what it held before the call (a loop reading its live length
    /// never ends), the collection is evaluated once, and a set, a queue and a dictionary's pairs add in
    /// the order they enumerate.
    /// </summary>
    [SkippableTheory]
    [InlineData("var l = new List<int> { 1, 2 }; l.AddRange(l); return string.Join(\",\", l);")] // "1,2,1,2"
    [InlineData("var l = new List<int> { 1, 2, 3 }; l.InsertRange(1, l); return string.Join(\",\", l);")] // "1,1,2,3,2,3"
    [InlineData("int calls = 0; int[] Items() { calls++; return new[] { 1, 2 }; } var l = new List<int>(); l.AddRange(Items()); l.InsertRange(0, Items()); return calls + \"|\" + string.Join(\",\", l);")] // "2|1,2,1,2"
    [InlineData("var l = new List<int>(); l.AddRange(new SortedSet<int> { 3, 1 }); l.InsertRange(1, new Queue<int>(new[] { 7, 8 })); return string.Join(\",\", l);")] // "1,7,8,3"
    [InlineData("var l = new List<KeyValuePair<string, int>>(); l.AddRange(new Dictionary<string, int> { [\"a\"] = 1 }); return l.Count + \"|\" + l[0].Key;")] // "1|a"
    public void ARange_KeepsWhatTheSpreadAnsweredRight(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
