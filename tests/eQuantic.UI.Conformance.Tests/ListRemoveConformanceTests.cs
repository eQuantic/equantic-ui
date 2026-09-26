using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>List&lt;T&gt;.Remove</c> answers as .NET's does, on both sides (#400). It was lowered to
/// <c>((_idx = list.indexOf(x)) &gt;= 0 &amp;&amp; list.splice(_idx, 1))</c>, which assigns a name nothing
/// declares, so every call threw <c>ReferenceError: _idx is not defined</c> in a module and the build
/// said nothing: the documentation site's "you are here" never moved off the first heading. No case
/// here executed a List's Remove before.
/// </summary>
public class ListRemoveConformanceTests
{
    private const string Prelude = """
        public record Point(int X, int Y);
        public record struct Cell(int Row, int Column);
        """;

    [SkippableTheory]
    // A present item leaves, and the list is one shorter.
    [InlineData("var list = new List<int> { 0, 1, 2 }; list.Remove(1); return list.Count;")]                           // 2
    // It answers a bool: true for an item that was there, false for one that was not.
    [InlineData("var list = new List<int> { 0, 1, 2 }; return list.Remove(1) + \"|\" + list.Remove(5) + \"|\" + string.Join(\",\", list);")] // "True|False|0,2"
    // Only the first equal item leaves, and the item expression is evaluated once.
    [InlineData("int calls = 0; int Next() { calls++; return 1; } var list = new List<int> { 1, 1 }; var removed = list.Remove(Next()); return removed + \"|\" + calls + \"|\" + list.Count;")] // "True|1|1"
    // The list is evaluated before the item, as C# evaluates a call.
    [InlineData("var order = \"\"; var list = new List<int> { 3 }; List<int> L() { order += \"l\"; return list; } int I() { order += \"i\"; return 3; } L().Remove(I()); return order + list.Count;")] // "li0"
    // Equal as EqualityComparer<T>.Default says: a record by value, a string by its text, NaN by NaN.
    [InlineData("var list = new List<Point> { new Point(1, 2), new Point(3, 4) }; return list.Remove(new Point(3, 4)) + \"|\" + list.Count;")] // "True|1"
    [InlineData("var list = new List<string> { \"a\", \"b\" }; return list.Remove(string.Concat(\"b\", \"\")) + \"|\" + list.Count;")]        // "True|1"
    [InlineData("var list = new List<double> { double.NaN, 1 }; return list.Remove(double.NaN) + \"|\" + list.Count;")]                     // "True|1"
    [InlineData("var list = new List<int>(); return list.Remove(0);")]                                                                     // false
    // A value tuple compares element by element, as its Equals does, where the default comparison of
    // two arrays would take them by reference (found in review, #421); a struct compares by value.
    [InlineData("var list = new List<(int, string)> { (1, \"a\"), (2, \"b\") }; return list.Remove((2, \"b\")) + \"|\" + list.Count;")]     // "True|1"
    [InlineData("var list = new List<(double, int)> { (double.NaN, 1) }; return list.Remove((double.NaN, 1)) + \"|\" + list.Count;")]        // "True|0"
    [InlineData("var list = new List<(Point, (int, int))> { (new Point(1, 2), (3, 4)) }; return list.Remove((new Point(1, 2), (3, 4))) + \"|\" + list.Count;")] // "True|0"
    [InlineData("var list = new List<Cell> { new Cell(1, 2) }; return list.Remove(new Cell(1, 2)) + \"|\" + list.Count;")]                   // "True|0"
    // Through ICollection<T>, the value may be a HashSet or a LinkedList when the call runs, and each
    // removes as it does when called directly (found in review, #421).
    [InlineData("ICollection<int> c = new HashSet<int> { 1, 2 }; var removed = c.Remove(1); return removed + \"|\" + c.Count + \"|\" + c.Remove(5);")] // "True|1|False"
    [InlineData("var linked = new LinkedList<int>(new[] { 1, 2, 1 }); ICollection<int> c = linked; var removed = c.Remove(1); return removed + \"|\" + linked.Count + \"|\" + linked.First.Value;")] // "True|2|2"
    [InlineData("ICollection<int> c = new List<int> { 1, 2 }; return c.Remove(2) + \"|\" + c.Count;")]                                     // "True|1"
    // A SortedSet through its own remove (found in review, #421). A dictionary's pairs are held in the
    // runtime's own spec, since a KeyValuePair built by hand does not cross yet (#433).
    [InlineData("var sorted = new SortedSet<int>(); sorted.Add(3); sorted.Add(1); sorted.Add(2); ICollection<int> c = sorted; return c.Remove(2) + \"|\" + sorted.Count + \"|\" + sorted.Contains(2);")] // "True|2|False"
    // A Dictionary with a primitive key is a plain object here, and through
    // ICollection<KeyValuePair<K, V>> it removes the pair whose key it holds with an equal value, a
    // tuple value compared by value (found in review, #421). Pairs come from a dictionary, since one
    // built by hand does not cross yet (#433).
    [InlineData("var dict = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }; var pairs = new List<KeyValuePair<string, int>>(dict); ICollection<KeyValuePair<string, int>> c = dict; return c.Remove(pairs[1]) + \"|\" + dict.Count + \"|\" + dict.ContainsKey(\"b\");")] // "True|1|False"
    [InlineData("var dict = new Dictionary<string, (int, int)> { [\"a\"] = (1, 2) }; var same = new List<KeyValuePair<string, (int, int)>>(new Dictionary<string, (int, int)> { [\"a\"] = (1, 2) }); var other = new List<KeyValuePair<string, (int, int)>>(new Dictionary<string, (int, int)> { [\"a\"] = (1, 3) }); ICollection<KeyValuePair<string, (int, int)>> c = dict; return c.Remove(other[0]) + \"|\" + c.Remove(same[0]) + \"|\" + dict.Count;")] // "False|True|0"
    // A list of pairs compares each pair's key and value, as a pair's Equals does.
    [InlineData("var pairs = new List<KeyValuePair<string, int>>(new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 }); var copy = new List<KeyValuePair<string, int>>(new Dictionary<string, int> { [\"b\"] = 2, [\"c\"] = 2 }); return pairs.Remove(copy[1]) + \"|\" + pairs.Remove(copy[0]) + \"|\" + pairs.Count;")] // "False|True|1"
    // A nullable tuple and an anonymous type compare by value too, as EqualityComparer<T>.Default does.
    [InlineData("var list = new List<(int, int)?> { (1, 2), null }; return list.Remove((1, 2)) + \"|\" + list.Remove(null) + \"|\" + list.Count;")] // "True|True|0"
    [InlineData("var list = new[] { new { X = 1 }, new { X = 2 } }.ToList(); return list.Remove(new { X = 2 }) + \"|\" + list.Count;")] // "True|1"
    public void ListRemove_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
