using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for advanced switch-expression patterns: nested <c>var</c> bindings (property and
/// positional subpatterns), list patterns (empty / fixed-length / leading + trailing around a slice),
/// and relational + <c>and</c> binding. Each evaluates to a primitive, compared against real .NET.
/// </summary>
public class PatternConformanceTests
{
    [SkippableTheory]
    // Relational + `and` binding.
    [InlineData("5 switch { > 0 and var n => n, _ => -1 }", "")]                          // -> 5
    [InlineData("-3 switch { < 0 and var n => -n, _ => 0 }", "")]                          // -> 3
    // List patterns over int[].
    [InlineData("(new int[]{}) switch { [] => -1, _ => 0 }", "")]                          // -> -1
    [InlineData("(new int[]{7,8,9}) switch { [] => -1, [var a, ..] => a, }", "")]          // head bind -> 7
    [InlineData("(new int[]{7,8,9}) switch { [.., var z] => z, _ => -1 }", "")]            // tail bind -> 9
    [InlineData("(new int[]{4,5}) switch { [var a, var b] => a + b, _ => -1 }", "")]       // fixed length -> 9
    [InlineData("(new int[]{1,2,3,4}) switch { [var a, .., var z] => a + z, _ => -1 }", "")] // -> 5
    [InlineData("(new int[]{1,2}) switch { [var a, var b, var c] => 1, _ => 0 }", "")]     // wrong length -> 0
    // Tuple positional binding (tuples are arrays in JS, so index access is correct).
    [InlineData("(3, 4) switch { (var a, var b) => a * b, }", "")]                         // -> 12
    public void Patterns_MatchDotNet(string expression, string prelude)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, prelude);
    }

    private const string RecordPrelude = "record Pt(int X, int Y);";

    /// <summary>Property pattern with a nested <c>var</c> binding over a record (object) — access is by
    /// member name, so it conforms.</summary>
    [SkippableTheory]
    [InlineData("(new Pt(0, 7)) switch { { X: 0, Y: var y } => y, _ => -1 }")]             // -> 7
    [InlineData("(new Pt(2, 9)) switch { { X: 0, Y: var y } => y, { X: var x } => x, _ => -1 }")] // -> 2
    // Positional pattern over a RECORD must read by Deconstruct member (`.x`/`.y`), not by index — a
    // record is a plain object at runtime, so `$s[0]` would be undefined.
    [InlineData("(new Pt(0, 7)) switch { (0, var y) => y, _ => -1 }")]                     // -> 7
    [InlineData("(new Pt(3, 4)) switch { (var a, var b) => a + b, _ => -1 }")]             // -> 7
    public void PropertyPatterns_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, RecordPrelude);
    }

    private const string NestedPrelude = "record Pt(int X, int Y); record Bag(List<int> Items, Pt? Corner);";

    /// <summary>
    /// An EXTENDED property pattern, <c>{ Items.Count: &gt; 1 }</c>, is <c>{ Items: { Count: &gt; 1 } }</c>:
    /// each member on the path is named as it would be alone, and a null on the way answers false, as
    /// C#'s does. The whole path was lower-cased as one name, so the code diff's
    /// <c>{ Changes.Count: &gt; 0 }</c> read <c>changes.Count</c>, undefined, and its step to the next
    /// change did nothing; and a null member on the path threw.
    /// </summary>
    [SkippableTheory]
    [InlineData("(new Bag(new List<int> { 1, 2 }, null)) switch { { Items.Count: > 1 } => 1, _ => 0 }")]                // -> 1
    [InlineData("(new Bag(new List<int> { 1 }, null)) switch { { Corner.X: 0 } => 1, _ => 0 }")]                       // -> 0: a null on the path
    [InlineData("(new Bag(new List<int>(), new Pt(0, 5))) switch { { Corner.X: 0, Corner.Y: var y } => y, _ => -1 }")] // -> 5
    [InlineData("new Bag(new List<int> { 3 }, null) is { Items.Count: 1, Corner: null }")]                            // -> true
    public void ExtendedPropertyPatterns_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, NestedPrelude);
    }

    /// <summary>
    /// A type with nothing bound (C# 9's type pattern) tests the type, as a declaration pattern does:
    /// it had no case, so `o is int or long`, `int => …` and `case int:` were all `false` (#482). A
    /// long is a BigInt here, and was asked whether it was a number.
    /// </summary>
    [SkippableTheory]
    [InlineData("object o = 5L; return o is int or long;")]                                                  // true
    [InlineData("object o = 5; return o switch { int => \"int\", string => \"text\", _ => \"other\" };")]     // "int"
    [InlineData("object o = \"x\"; return o switch { int => \"int\", string => \"text\", _ => \"other\" };")] // "text"
    [InlineData("object o = 5; switch (o) { case long: return \"long\"; case int: return \"int\"; default: return \"other\"; }")] // "int"
    [InlineData("object o = 2.5; return o is int;")]                                                         // false
    [InlineData("object o = 2.5; return o is double;")]                                                      // true
    [InlineData("object o = 1.5m; return o is int or decimal;")]                                             // true
    [InlineData("object o = 1.5m; return o is double;")]                                                     // false
    [InlineData("object o = 7L; return o is System.Int64;")]                                                 // true
    public void ABareTypePattern_TestsTheType(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// A collection's <c>Count</c> in a property pattern reads as the collection's class answers it: a
    /// Set's <c>size</c>, the runtime queue's <c>count</c>, and an open face's helper. It read
    /// <c>length</c> on each, undefined, so the pattern was false in the browser alone (#516).
    /// </summary>
    [SkippableTheory]
    [InlineData("var s = new HashSet<int> { 1, 2 }; return s is { Count: > 1 };")]                                  // -> true
    [InlineData("var q = new Queue<int>(new[] { 7 }); return q is { Count: 1 };")]                                  // -> true
    [InlineData("var t = new Stack<int>(new[] { 1, 2, 3 }); return t is { Count: 3 } ? t.Peek() : -1;")]            // -> 3
    [InlineData("ICollection<int> c = new HashSet<int> { 4 }; return c is { Count: 1 };")]                          // -> true
    [InlineData("var d = new SortedSet<int> { 2, 1 }; return d is { Count: 2, Min: 1 };")]                          // -> true
    public void ACollectionsCountInAPattern_ReadsAsItsClassAnswersIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// <c>x is Limits.Max</c> parses as the type test and binds as a constant pattern: it was
    /// answered <c>x != null</c>, true for every number (#451). An enum's member is the member.
    /// </summary>
    [SkippableTheory]
    [InlineData("int x = 3; return x is Limits.Max;")]                    // false
    [InlineData("int x = 10; return x is Limits.Max;")]                   // true
    [InlineData("string s = \"on\"; return s is Limits.Label;")]          // true
    [InlineData("var l = Level.High; return l is Level.High;")]           // true
    [InlineData("var l = Level.Low; return l is Level.High;")]            // false
    [InlineData("var l = Level.High; return l is (Level)1;")]             // true
    public void IsANamedConstant_ComparesToItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements,
            "public static class Limits { public const int Max = 10; public const string Label = \"on\"; }\npublic enum Level { Low, High }");
    }

    /// <summary>
    /// A constant in a pattern is tested as a constant pattern tests it, by its value, in either
    /// spelling: a long is a BigInt, a decimal an object compared by value, a NaN matches a NaN, and
    /// a null any absence. A char is one code unit, so a longer string is no char.
    /// </summary>
    [SkippableTheory]
    [InlineData("long x = 5; return x is Limits.Five;")]                                   // true
    [InlineData("object o = 5L; return o is Limits.Five;")]                                // true
    [InlineData("object o = 1.5m; return o is Limits.Rate;")]                              // true
    [InlineData("object o = 1.50m; return o is 1.5m;")]                                    // true
    [InlineData("object o = 2.5m; return o is Limits.Rate;")]                              // false
    [InlineData("double d = double.NaN; return d is Limits.NotANumber;")]                  // true
    [InlineData("double d = double.NaN; return d is double.NaN;")]                         // true
    [InlineData("string s = null; return s is Limits.Nothing;")]                           // true
    [InlineData("object o = \"hello\"; return o switch { char => 1, string => 2, _ => 3 };")] // 2
    [InlineData("object o = 'h'; return o switch { char => 1, string => 2, _ => 3 };")]      // 1
    public void AConstantInAPattern_IsTestedByItsValue(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements,
            "public static class Limits { public const long Five = 5; public const decimal Rate = 1.5m; "
            + "public const double NotANumber = double.NaN; public const string Nothing = null; }");
    }
}
