using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An interpolation hole's alignment is the number it is, whatever the source spells it with (#834).
/// C# takes a constant expression there and nothing else, and the hole was written with the
/// expression's SOURCE TEXT: <c>$"{name,Widths.Name}"</c> wrote <c>Widths.Name</c>, which is no member
/// of the twin, whose static is <c>name</c>, so the text was never padded and nothing said so. A
/// constant of a referenced project was a name the module did not import at all. A literal was the
/// only spelling that worked, and a local constant worked by the accident of its name.
/// </summary>
public class InterpolationAlignmentConformanceTests
{
    private const string Prelude =
        "public static class Widths { public const int Name = 6; public const int Wide = Name * 2; } " +
        "public enum Kind { A, B }";

    [SkippableTheory]
    // A constant by its name, either way round, and a constant built from another.
    [InlineData("string name = \"ab\"; return $\"{name,Widths.Name}|\";")]
    [InlineData("string name = \"ab\"; return $\"{name,-Widths.Name}|\";")]
    [InlineData("string name = \"ab\"; return $\"{name,Widths.Wide}|\";")]
    // A constant expression: a sum, and a negated one.
    [InlineData("string name = \"ab\"; return $\"{name,Widths.Name + 2}|{name,-(Widths.Name + 1)}|\";")]
    // With a format beside it, and for a value that formats itself.
    [InlineData("double d = 1.5; return $\"{d,Widths.Name:F2}|{d,-Widths.Name:F1}|\";")]
    [InlineData("var kind = Kind.B; return $\"{kind,Widths.Name}|{kind,-Widths.Name:G}|\";")]
    [InlineData("string? none = null; return $\"{none,Widths.Name}|\";")]
    // An interpolated string handed to a builder appends each hole with its own padding.
    [InlineData("string name = \"ab\"; return new System.Text.StringBuilder().Append($\"{name,Widths.Name}|\").Append($\"{name,-Widths.Name}|\").ToString();")]
    // The spellings that already held: a literal, and a local constant.
    [InlineData("string name = \"ab\"; return $\"{name,6}|{name,-6}|\";")]
    [InlineData("const int local = 4; string name = \"ab\"; return $\"{name,local}|{name,-local}|\";")]
    public void AnAlignment_PadsToTheNumberItIs_AsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
