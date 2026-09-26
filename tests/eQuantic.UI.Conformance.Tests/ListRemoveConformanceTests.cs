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
    public void ListRemove_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
