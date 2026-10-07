using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A mutable struct and a value tuple are copied where C# copies them, never shared (#560). C# copies
/// such a value on every assignment, argument, return and boxing, and JavaScript handed the same object
/// over, so a write through one name showed through every other. The twin copies on write: the
/// variable, parameter, field or element that holds the value is given a copy before a member is
/// written or a method that writes the value's state is called, and <c>this</c> is copied where it
/// leaves its struct's member. A capture still reads the variable, as C#'s closure does.
/// </summary>
public class ValueCopyConformanceTests
{
    private const string Types =
        "public struct Pt { public int X; public int Y; public void Move(int by) { X += by; } public int Sum() => X + Y; }\n"
        + "public struct Line { public Pt A; public Pt B; }\n"
        + "public record Holder { public Pt P; public Pt Prop { get; set; } }\n"
        + "public struct Snapper { public int V; public int Snap() { var copy = this; V = 5; return copy.V; } }\n"
        + "public struct Pair { public int A; public int B; static void Swap(ref int x, ref int y) { var t = x; x = y; y = t; } public void Flip() => Swap(ref A, ref B); }\n";

    [SkippableTheory]
    // A tuple and a struct read into another variable keep their value when the first is written.
    [InlineData("var t = (1, 2); var u = t; t.Item1 = 9; return u.Item1 + \"|\" + t.Item1;")]                 // "1|9"
    [InlineData("var a = new Pt { X = 1, Y = 2 }; var b = a; a.X = 9; return b.X + \"|\" + a.X;")]             // "1|9"
    // An argument is the callee's own copy.
    [InlineData("void Bump(Pt p) { p.X += 10; } var a = new Pt { X = 1 }; Bump(a); return a.X;")]             // 1
    // A method that writes the value's state writes the caller's copy only.
    [InlineData("var c = new Pt { X = 1 }; var d = c; c.Move(4); return c.X + \"|\" + d.X + \"|\" + c.Sum();")] // "5|1|5"
    // A value inside a value, an element of an array and a struct field of a class.
    [InlineData("var l = new Line(); var m = l; l.A.X = 5; return l.A.X + \"|\" + m.A.X;")]                   // "5|0"
    [InlineData("var arr = new Pt[2]; var p = arr[0]; arr[0].X = 7; arr[1].Move(2); return arr[0].X + \"|\" + p.X + \"|\" + arr[1].X;")] // "7|0|2"
    [InlineData("var h = new Holder(); var q = h.P; h.P.X = 3; h.P.Y++; return h.P.X + \"|\" + h.P.Y + \"|\" + q.X;")] // "3|1|0"
    // A boxing is a copy, and a capture reads the variable.
    [InlineData("var a = new Pt { X = 1 }; object o = a; a.X = 2; return ((Pt)o).X + \"|\" + a.X;")]          // "1|2"
    [InlineData("var t = (1, 2); System.Func<int> f = () => t.Item1; t.Item1 = 9; return f();")]              // 9
    // `this` leaving its member is a copy, and a mutating call on a property's value runs on a copy.
    [InlineData("var s = new Snapper { V = 1 }; return s.Snap() + \"|\" + s.V;")]                              // "1|5"
    [InlineData("var h = new Holder(); h.Prop = new Pt { X = 1 }; h.Prop.Move(5); return h.Prop.X;")]         // 1
    // A mutating call on a foreach variable runs on a copy, and a deconstruction writes its values' copies.
    [InlineData("var arr = new Pt[] { new Pt { X = 1 } }; foreach (var p in arr) p.Move(5); return arr[0].X;")] // 1
    [InlineData("var a = new Pt { X = 1, Y = 2 }; var b = a; (a.X, a.Y) = (5, 6); return b.X + \"|\" + a.X;")] // "1|5"
    // A method that writes the value through a ref argument writes the caller's copy only.
    [InlineData("var p = new Pair { A = 1, B = 2 }; var q = p; p.Flip(); return q.A + \"|\" + p.A;")]       // "1|2"
    // A write handed to a builder as its value is one argument.
    [InlineData("var sb = new System.Text.StringBuilder(); var p = new Pt { X = 3 }; var q = p; sb.Append(p.X++); return sb + \"|\" + p.X + \"|\" + q.X;")] // "3|4|3"
    public void AMutableValue_IsCopiedWhereCSharpCopiesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Types);
    }
}
