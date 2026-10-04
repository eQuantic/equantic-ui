using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>string.Join</c> answers as .NET's does, on both sides. It lowered to <c>values.join(separator)</c>,
/// so a sequence that is not an array threw, a set, a linked list and a sequence behind an interface
/// alike (#429); each value was written by JavaScript's <c>toString</c>, a bool in lower case, an enum by
/// its key, a float by the double's digits and 1E+21 as <c>1e+21</c> (#441); the values a params array
/// takes one by one became <c>1.join(',')</c>, which does not parse; and a null separator wrote "null"
/// between them. Each value is written now as .NET's <c>ToString</c> writes it, a null one as nothing.
/// </summary>
public class StringJoinConformanceTests
{
    private const string Prelude = """
        public record Point(int X, int Y);
        public enum Day { Monday, Friday }
        [System.Flags] public enum Mode { None = 0, A = 1, B = 2 }
        """;

    [SkippableTheory]
    // The rows of #429: any sequence, whatever its static type says.
    [InlineData("return string.Join(\",\", new HashSet<int> { 1, 2 });")]
    [InlineData("ICollection<int> c = new HashSet<int> { 1, 2 }; return string.Join(\",\", c);")]
    [InlineData("IEnumerable<int> c = new HashSet<int> { 1, 2 }; return string.Join(\",\", c);")]
    [InlineData("return string.Join(\",\", new LinkedList<int>(new[] { 1, 2 }));")]
    [InlineData("IEnumerable<int> c = new List<int> { 1, 2 }; return string.Join(\",\", c);")]
    [InlineData("return string.Join(\",\", new Queue<int>(new[] { 1, 2 })) + \"|\" + string.Join(\",\", new Stack<int>(new[] { 1, 2 })) + \"|\" + string.Join(\",\", new SortedSet<int> { 2, 1 });")]
    [InlineData("return string.Join(\",\", new[] { 1, 2 }.Select(x => x > 1));")]
    [InlineData("return string.Join(\", \", new[] { \"x\" }.Concat(new[] { \"y\" }));")]
    [InlineData("var d = new Dictionary<bool, int> { [true] = 1, [false] = 0 }; return string.Join(\",\", d.Keys);")]
    [InlineData("return string.Join(\",\", new Dictionary<string, bool> { [\"a\"] = true }.Values);")]
    // An empty sequence, and nulls, written as nothing.
    [InlineData("return string.Join(\",\", new List<int>());")]
    [InlineData("return string.Join(\",\", new string[] { null });")]
    [InlineData("return string.Join(\",\", new object[] { null, \"a\" });")]
    [InlineData("return string.Join(\",\", new int?[] { 1, null, 3 });")]
    [InlineData("return string.Join(\",\", \"a\", null, \"b\");")]
    public void Join_ReadsAnySequence(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }

    [SkippableTheory]
    // The rows of #441: a bool, an enum, a float and a large double, written as .NET writes them.
    [InlineData("return string.Join(\",\", new[] { true, false });")]
    [InlineData("return string.Join(\",\", new[] { Day.Friday, Day.Monday });")]
    [InlineData("return string.Join(\",\", new List<float> { 0.1f, 1e21f });")]
    [InlineData("return string.Join(\",\", new[] { 1e21, 0.1, -0.0, double.NaN });")]
    // A flags enum's set flags, a nullable enum, a nullable bool, a record's text.
    [InlineData("return string.Join(\";\", new[] { Mode.A | Mode.B, Mode.None });")]
    [InlineData("return string.Join(\",\", new List<Day?> { Day.Monday, null });")]
    [InlineData("return string.Join(\",\", new bool?[] { true, null });")]
    [InlineData("return string.Join(\";\", new[] { new Point(1, 2) });")]
    // A long, a decimal, a char and a double through the generic overload.
    [InlineData("return string.Join(\",\", new List<long> { 1L, long.MaxValue }) + \"|\" + string.Join(\",\", new[] { 1.0m, 1.00m }) + \"|\" + string.Join(\",\", new[] { 'a', 'b' });")]
    [InlineData("return string.Join<double>(\",\", new List<double> { 0.5, 1e-7 });")]
    // Values passed one by one, each written by its own type.
    [InlineData("return string.Join(\",\", 1, 2);")]
    [InlineData("return string.Join(\",\", true, 0.1f, Day.Friday);")]
    [InlineData("return string.Join(\",\", new object[] { true, 1.5, null, 'c', 2L });")]
    // A char separator, and a null one, which writes nothing between the values.
    [InlineData("return string.Join(';', new List<bool> { true, false });")]
    [InlineData("return string.Join('-', \"a\", \"b\") + \"|\" + string.Join('-', 1, true);")]
    [InlineData("return string.Join((string)null, new[] { 1, 2 });")]
    [InlineData("string sep = null; return string.Join(sep, new List<string> { \"a\", \"b\" });")]
    [InlineData("return string.Join(\",\", new[] { \"a\", \"b\", \"c\" }, 1, 2);")]
    // A null sequence is refused by the name of the overload's parameter.
    [InlineData("try { return string.Join(\",\", (object[])null); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return string.Join(\",\", (string[])null); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return string.Join(\",\", (IEnumerable<int>)null); } catch (Exception e) { return e.Message; }")]
    public void Join_WritesEachValueAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
