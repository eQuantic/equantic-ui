using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for the broader user value types beyond positional records: <b>body records</b>
/// (declared with property members, constructed via an object initializer) and <b>plain structs</b>.
/// Both emit as named JS classes; construction maps the initializer to the constructor by member order,
/// with per-member defaults for omitted ones.
/// </summary>
public class ValueTypeConformanceTests
{
    private const string Person =
        "public record Person { public string Name { get; set; } public int Age { get; set; } " +
        "public string Greeting() => \"Hi \" + Name; }";

    [SkippableTheory]
    [InlineData("new Person { Name = \"Ann\", Age = 30 }.Greeting()")]   // "Hi Ann"
    [InlineData("new Person { Name = \"Ann\", Age = 30 }.Age")]          // 30
    [InlineData("new Person { Name = \"X\" }.Age")]                       // 0 (omitted -> default)
    [InlineData("new Person { Name = \"A\", Age = 1 } == new Person { Name = \"A\", Age = 1 }")] // true
    [InlineData("new Person { Name = \"A\", Age = 1 } == new Person { Name = \"B\", Age = 1 }")] // false
    public void BodyRecord_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Person);
    }

    private const string Pt =
        "public struct Pt { public int X; public int Y; public int Sum() => X + Y; }";

    [SkippableTheory]
    [InlineData("new Pt { X = 3, Y = 4 }.Sum()")]   // 7 (instance method on a struct)
    [InlineData("new Pt { X = 3, Y = 4 }.X")]       // 3
    [InlineData("new Pt().X")]                       // 0 (default-constructed)
    [InlineData("new Pt { X = 1, Y = 2 }.Equals(new Pt { X = 1, Y = 2 })")] // true (value equality)
    [InlineData("new Pt { X = 1, Y = 2 }.Equals(new Pt { X = 9, Y = 9 })")] // false
    public void PlainStruct_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Pt);
    }

    private const string Mixed = "public record R(int X) { public int Y { get; set; } public int Sum() => X + Y; }";

    [SkippableTheory]
    [InlineData("new R(5) { Y = 3 }.Sum()")]  // 8 (positional param + body property)
    [InlineData("new R(5) { Y = 3 }.X")]      // 5
    [InlineData("new R(5).Y")]                // 0 (Y omitted)
    public void MixedRecord_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Mixed);
    }
}
