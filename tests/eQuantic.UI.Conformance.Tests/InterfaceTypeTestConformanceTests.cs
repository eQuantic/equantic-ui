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
        public interface IShape { int Sides { get; } }
        public interface IBox<T> { T Item { get; } }
        public record Square : IShape { public int Sides => 4; }
        public record Plain { public int Sides => 0; }
        public record Tile : Square { }
        public record Corner(int X) : IShape { public int Sides => 2; }
        public struct Point : IShape { public int Sides => 1; }
        public record Held : System.IDisposable { public int Disposed; public void Dispose() => Disposed++; }
        public record Free { public int Disposed; public void Dispose() => Disposed++; }
        """;

    [SkippableTheory]
    [InlineData("object o = new Square(); return o is IShape;")]                                       // true
    [InlineData("object o = new Plain(); return o is IShape;")]                                        // false: it has the member, not the interface
    [InlineData("object o = new Tile(); return o is IShape s ? s.Sides : -1;")]                        // 4: its base implements it
    [InlineData("object o = new Corner(1); return o is IShape s ? s.Sides : -1;")]                     // 2: a positional record
    [InlineData("object o = new Point(); return o is IShape s ? s.Sides : -1;")]                       // 1: a boxed struct
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
        ConformanceRunner.AssertStatementsSameAsDotNet(program, Prelude);
    }

    /// <summary>
    /// A test the browser cannot answer is refused (EQ2015) rather than guessed: an interface a string, a number
    /// or an array implements, which none of them could say, and one with type arguments, which JavaScript erases.
    /// </summary>
    [SkippableTheory]
    [InlineData("object o = 1; return o is System.IComparable;")]
    [InlineData("object o = \"x\"; return o is System.Collections.IEnumerable;")]
    [InlineData("object o = new int[0]; return o is System.Collections.Generic.IEnumerable<int>;")]
    [InlineData("object o = new Square(); return o is IBox<int>;")]
    public void ATypeTestTheBrowserCannotAnswer_IsRefused(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsRefusedByTheBuild(program, "EQ2015", Prelude);
    }
}
