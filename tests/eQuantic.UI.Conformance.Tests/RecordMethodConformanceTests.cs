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
    // A declared default holding a line break, folded or written plain, and filled into a named call that
    // skips it: the text was quoted with its backslashes and apostrophes escaped only, and a raw line break
    // inside a JavaScript string literal is a syntax error that took the whole module with it.
    [InlineData("public record Sep(string Joined = \"a\" + \"\\n\", char Line = '\\n', string Plain = \"\\r\\n\");", "new Sep().Joined + new Sep().Line + new Sep().Plain")]
    [InlineData("public record Sep(string Joined = \"a\" + \"\\n\", char Line = '\\n', string Plain = \"\\r\\n\");", "new Sep(Plain: \"z\").Joined + new Sep(Plain: \"z\").Line")]
    // A declared default gives the value its declaration does, whatever expression wrote it, where a named call
    // skips it: `-1` is a minus over a literal, and an optional `int SourceLine = -1` once constructed as null,
    // which a diff's gap row read as the other document's first line.
    [InlineData("public record Filler(int BeforeLine, int Rows, int SourceLine = -1, double Scale = -0.5, int Mask = 1 << 3, string Tag = \"a\" + \"b\", string? Label = null);", "new Filler(3, 1, Label: \"h\").SourceLine + \"|\" + new Filler(3, 1, Label: \"h\").Scale + \"|\" + new Filler(3, 1, Label: \"h\").Mask + \"|\" + new Filler(3, 1, Label: \"h\").Tag")]
    public void RecordMethods_MatchDotNet(string prelude, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, prelude);
    }
}
