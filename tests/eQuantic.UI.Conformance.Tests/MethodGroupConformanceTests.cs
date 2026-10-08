using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A method group is the delegate C# makes, both sides executed through the module graph an app's
/// build writes:
/// <list type="bullet">
/// <item>its receiver is read once, when the delegate is made: one that is a call ran twice, once to
/// read the method and once to bind it (#619);</item>
/// <item><c>base.M</c> calls the base's method on this object: bound to <c>super</c>, the module did
/// not parse (#655);</item>
/// <item>an extension's method lives on its home: bound to its receiver, it named a member the receiver
/// never had (#655).</item>
/// </list>
/// </summary>
public class MethodGroupConformanceTests
{
    private const string Declarations = """
        public class Counter { public int Made; public Counter Make() { Made++; return this; } public int Value() => Made; }
        public class Animal { public virtual string Sound() => "..."; }
        public class Dog : Animal { public override string Sound() => "woof"; public System.Func<string> Quiet() => base.Sound; }
        public static class Ext
        {
            public static string Twice(this string s) => s + s;
            public static string Label(this Counter c) => "made " + c.Made;
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a receiver that is a call", "var c = new Counter(); System.Func<int> read = c.Make().Value; return read() + c.Made;"),
        ("a group on base", "var d = new Dog(); return d.Quiet()() + d.Sound();"),
        ("a group of an extension", "var s = \"ab\"; System.Func<string> twice = s.Twice; return twice();"),
        ("an extension's receiver that is a call", "var c = new Counter(); System.Func<string> label = c.Make().Label; return label() + c.Made;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AMethodGroup_IsTheDelegateCSharpMakes(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Declarations, typeAnnotations, Cases);
}
