using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A name in the browser is the name C# means, both sides executed, one case at a time:
/// <list type="bullet">
/// <item>a C# local named like a global the emitted code reads hid it from the lowering beside it
/// (<c>crypto</c>, <c>undefined</c>, <c>Math</c>), and one named like a binding a lowering declares read
/// that binding (<c>_sum</c>, <c>key</c>, <c>a</c>, <c>x</c>): both are renamed apart now (#397);</item>
/// <item>a label, a member and a <c>with</c> key written with the verbatim escape went out with it, or as
/// a word a module reserves (#467);</item>
/// <item>a value C# evaluates once, an argument of a LINQ call, ran once per element or twice (#657).</item>
/// </list>
/// </summary>
public class EmittedNameConformanceTests
{
    private const string Prelude = "public record R(int A, int B = 7, int C = 9); public record R2(int @class);";

    public static TheoryData<string, string> Cases() => new()
    {
        { "a local named crypto", "var crypto = \"xy\"; return Guid.NewGuid().ToString().Length + crypto.Length;" },
        { "a local named undefined", "int undefined = 5; var r = new R(1, C: 3); return r.B;" },
        { "a local named Math", "int Math = 3; return System.Math.Abs(-4) + Math;" },
        { "a captured _sum", "int _sum = 10; return new[] { 1, 2 }.Sum(v => v + _sum);" },
        { "a captured key", "var key = 10; return new[] { 1, 2, 3, 4 }.GroupBy(x => x % 2 + key).Count();" },
        { "a captured a", "int a = -1; return new[] { 3, 1, 2 }.OrderBy(x => x * a).First();" },
        { "a sequence named x", "var x = new[] { 2, 3 }; return new[] { 1, 2 }.Intersect(x).Count();" },
        { "Intersect's sequence, once", "int calls = 0; int[] Other() { calls++; return new[] { 2, 3 }; } var n = new[] { 1, 2, 3, 4 }.Intersect(Other()).Count(); return n * 10 + calls;" },
        { "Except's sequence, once", "int calls = 0; int[] Other() { calls++; return new[] { 2, 3 }; } var n = new[] { 1, 2, 3, 4 }.Except(Other()).Count(); return n * 10 + calls;" },
        { "Average's source, once", "int calls = 0; int[] Items() { calls++; return new[] { 1, 2, 3 }; } var avg = Items().Average(); return avg * 10 + calls;" },
        { "Sum's selector, once", "int calls = 0; Func<int, int> Make() { calls++; return v => v * 2; } var total = new[] { 1, 2, 3 }.Sum(Make()); return total * 10 + calls;" },
        { "GroupBy's key selector, once", "int calls = 0; Func<int, int> Key() { calls++; return v => v % 2; } var groups = new[] { 1, 2, 3 }.GroupBy(Key()).Count(); return groups * 10 + calls;" },
        { "a label named package", "int n = 0; package: for (int i = 0; i < 3; i++) { for (int j = 0; j < 3; j++) { if (j == 1) continue package; if (i == 2) break package; n++; } } return n;" },
        { "a member written @class", "return new R2(3).@class;" },
        { "a with key written @class", "return (new R2(3) with { @class = 5 }).ToString();" },
    };

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public void ANameInTheBrowser_IsTheNameCSharpMeans(string name, string statements)
    {
        _ = name;
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
