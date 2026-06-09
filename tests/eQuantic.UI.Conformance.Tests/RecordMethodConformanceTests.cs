using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for records emitted as named JS classes carrying user instance methods — the capability
/// a plain-object representation can't provide. Also covers the .NET record <c>ToString</c> (default and
/// overridden). Value semantics (==, with, Contains, deconstruction) are covered elsewhere and must keep
/// passing under the class representation.
/// </summary>
public class RecordMethodConformanceTests
{
    [SkippableTheory]
    // Expression-bodied method reading positional members
    [InlineData("public record Point(int X, int Y) { public int Sum() => X + Y; }",
        "new Point(3, 4).Sum()")]                                   // 7
    // Method with a parameter
    [InlineData("public record Point(int X, int Y) { public int AddTo(int n) => X + Y + n; }",
        "new Point(1, 2).AddTo(10)")]                               // 13
    // Block-bodied method with a local
    [InlineData("public record Point(int X, int Y) { public string Describe() { var s = X + Y; return \"sum=\" + s; } }",
        "new Point(2, 3).Describe()")]                              // "sum=5"
    // Method composing arithmetic
    [InlineData("public record Vec(int X, int Y) { public int LenSq() => X * X + Y * Y; }",
        "new Vec(3, 4).LenSq()")]                                   // 25
    // One method calling another on the same instance
    [InlineData("public record Point(int X, int Y) { public int Sum() => X + Y; public int Twice() => Sum() * 2; }",
        "new Point(2, 3).Twice()")]                                 // 10
    // Default record ToString
    [InlineData("public record Point(int X, int Y);",
        "new Point(1, 2).ToString()")]                              // "Point { X = 1, Y = 2 }"
    // Overridden ToString
    [InlineData("public record Tag(int X) { public override string ToString() => \"#\" + X; }",
        "new Tag(5).ToString()")]                                   // "#5"
    // Methods coexist with value equality
    [InlineData("public record Point(int X, int Y) { public int Sum() => X + Y; }",
        "new Point(1, 2) == new Point(1, 2) && new Point(1, 2).Sum() == 3")] // true
    public void RecordMethods_MatchDotNet(string prelude, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, prelude);
    }
}
