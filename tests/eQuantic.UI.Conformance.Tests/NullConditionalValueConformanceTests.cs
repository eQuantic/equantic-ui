using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A null-conditional read answers null where C# does, not JavaScript's undefined, both sides executed
/// through the module graph an app's build writes. An optional chain was undefined in the browser,
/// which a parameter tested with <c>is null</c>, a dictionary looking for null, and a chain held in a
/// field told apart from null, and so was a chain behind a guard, whose inner link the guard handed out
/// as it was (#633).
/// </summary>
public class NullConditionalValueConformanceTests
{
    private const string Declarations = """
        public class Person { public string? Name; public Person? Boss; public int Age; }
        public static class Probe { public static string Kind(string? s) => s is null ? "null" : "value"; }
        public class Holder { public string? Kept; }
        public static class Ext { public static string? Pick(this string? s) => null; }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("a read passed to a parameter", "Person? p = null; return Probe.Kind(p?.Name);"),
        ("a value read tested with is null", "Person? p = null; int? age = p?.Age; return age is null;"),
        ("a chain off a null member", "var p = new Person(); return Probe.Kind(p.Boss?.Name);"),
        ("a read that has a value", "var p = new Person { Name = \"ada\" }; return Probe.Kind(p?.Name) + p?.Name;"),
        ("a dictionary looking for null", "Person? p = null; var d = new System.Collections.Generic.Dictionary<string, string?> { [\"k\"] = p?.Name }; return d.ContainsValue(null);"),
        ("a field that holds a read", "Person? p = null; var h = new Holder { Kept = p?.Name }; return h.Kept is null;"),
        ("a chain behind a guard", "var p = new Person { Name = \"ada\" }; var d = new System.Collections.Generic.Dictionary<string, int?> { [\"k\"] = p?.Name.Pick()?.Length }; return d.ContainsValue(null);"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullConditionalAnswersNull_WhereItsValueIsUsed(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Declarations, typeAnnotations, Cases);
}
