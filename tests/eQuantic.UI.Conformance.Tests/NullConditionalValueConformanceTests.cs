using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A null-conditional read answers null where C# does, not JavaScript's undefined, and a method group
/// reads its receiver once, both sides executed through the module graph an app's build writes:
/// <list type="bullet">
/// <item>an optional chain was undefined in the browser, which a parameter tested with <c>is null</c>,
/// a dictionary looking for null, and a chain held in a field told apart from null (#633);</item>
/// <item>a method group bound to a receiver that is a call ran the call twice, once to read the method
/// and once to bind it (#619).</item>
/// </list>
/// </summary>
public class NullConditionalValueConformanceTests
{
    private const string Declarations = """
        public class Person { public string? Name; public Person? Boss; public int Age; }
        public static class Probe { public static string Kind(string? s) => s is null ? "null" : "value"; }
        public class Holder { public string? Kept; }
        public class Counter { public int Made; public Counter Make() { Made++; return this; } public int Value() => Made; }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a read passed to a parameter", "Person? p = null; return Probe.Kind(p?.Name);"),
        ("a value read tested with is null", "Person? p = null; int? age = p?.Age; return age is null;"),
        ("a chain off a null member", "var p = new Person(); return Probe.Kind(p.Boss?.Name);"),
        ("a read that has a value", "var p = new Person { Name = \"ada\" }; return Probe.Kind(p?.Name) + p?.Name;"),
        ("a dictionary looking for null", "Person? p = null; var d = new System.Collections.Generic.Dictionary<string, string?> { [\"k\"] = p?.Name }; return d.ContainsValue(null);"),
        ("a field that holds a read", "Person? p = null; var h = new Holder { Kept = p?.Name }; return h.Kept is null;"),
        ("a method group whose receiver is a call", "var c = new Counter(); System.Func<int> read = c.Make().Value; return read() + c.Made;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullConditionalAnswersNull_AndAMethodGroupReadsItsReceiverOnce(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Declarations, typeAnnotations, Cases);
}
