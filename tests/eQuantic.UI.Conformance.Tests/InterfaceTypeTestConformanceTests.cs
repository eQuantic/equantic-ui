using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type test against an interface answers what .NET answers off the type: whether the value's class
/// implements it, its bases included, whatever members the value has (#1464). It was <c>x != null</c>, so a
/// class that implements nothing passed, and the element engine's <c>(state as IDisposable)?.Dispose()</c>
/// called a <c>dispose</c> no state had.
/// </summary>
public class InterfaceTypeTestConformanceTests
{
    private const string Prelude = """
        interface IShape { int Sides { get; } }
        class Square : IShape { public int Sides => 4; }
        class Plain { public int Sides => 0; }
        class Tile : Square { }
        record Corner(int X) : IShape { public int Sides => 2; }
        sealed class Held : System.IDisposable { public int Disposed; public void Dispose() => Disposed++; }
        sealed class Free { public int Disposed; public void Dispose() => Disposed++; }
        """;

    [SkippableTheory]
    [InlineData("object o = new Square(); return o is IShape;")]                                       // true
    [InlineData("object o = new Plain(); return o is IShape;")]                                        // false: it has the member, not the interface
    [InlineData("object o = new Tile(); return o is IShape s ? s.Sides : -1;")]                        // 4: its base implements it
    [InlineData("object o = new Corner(1); return o is IShape s ? s.Sides : -1;")]                     // 2: a record
    [InlineData("object o = new Plain(); return (o as IShape)?.Sides ?? -1;")]                         // -1
    [InlineData("object o = \"text\"; return o is IShape;")]                                           // false
    [InlineData("object o = new Plain(); return o switch { IShape s => s.Sides, _ => -1 };")]          // -1: the arm does not take every value
    [InlineData("object o = new Square(); return o switch { IShape s => s.Sides, _ => -1 };")]         // 4
    [InlineData("var h = new Held(); object o = h; (o as System.IDisposable)?.Dispose(); return h.Disposed;")] // 1
    [InlineData("var f = new Free(); object o = f; (o as System.IDisposable)?.Dispose(); return f.Disposed;")] // 0: a Dispose with no IDisposable
    [InlineData("object o = new Free(); return o is System.IDisposable;")]                            // false
    public void ATypeTestAgainstAnInterface_AsksWhatTheClassImplements(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(program, Prelude);
    }
}
