using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A component that takes its parameters through its constructor, a plain one, one with a default and a
/// <c>params</c> list last, built and read on both sides, in TypeScript and in plain JavaScript, beside a
/// plain class, a record and a method whose parameter lists end the same way. The twin's constructor is
/// a signature the emitter writes by hand, and it wrote <c>tag?: any</c> and <c>size: any = 2</c> into
/// the plain module, which bun refused, so the app's bundle failed on a component that compiles in C#
/// (#1574).
/// </summary>
public class ComponentConstructorConformanceTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;
        public sealed class Tally(string tag, int size = 2, params int[] items) : StatelessComponent
        {
            public string Says() => tag + "|" + size + "|" + items.Length + "|" + string.Join(",", items);
            public override VisualNode Build(ComponentContext context) => new Text(Says(), TypeRole.BodyM);
        }
        public sealed class Badge : StatelessComponent
        {
            private readonly string _label;
            private readonly int[] _counts;
            public Badge(string label, params int[] counts) { _label = label; _counts = counts; }
            public string Says() => _label + "|" + _counts.Length;
            public override VisualNode Build(ComponentContext context) => new Text(Says(), TypeRole.BodyM);
        }
        public sealed class Line
        {
            private readonly string _tag;
            private readonly int[] _items;
            public Line(string tag, int size = 2, params int[] items) { _tag = tag + size; _items = items; }
            public string Says() => _tag + "|" + _items.Length;
        }
        public sealed record Row(string Tag, int Size = 2, params int[] Items)
        {
            public string Says() => Tag + Size + "|" + Items.Length;
        }
        public static class Sums
        {
            public static string Of(string tag, int size = 2, params int[] items) => tag + size + "|" + items.Length;
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("every parameter passed, the params list as its elements", "return new Tally(\"t\", 3, 4, 5).Says();"),
        ("the default and the params list left out, which .NET passes as an empty array", "return new Tally(\"t\").Says();"),
        ("the params list passed as an array", "return new Tally(\"t\", 3, new[] { 7 }).Says();"),
        ("an explicit constructor ending in params, with none passed", "return new Badge(\"b\").Says();"),
        ("an explicit constructor ending in params, with two passed", "return new Badge(\"b\", 1, 2).Says();"),
        ("a plain class's constructor ending in params", "return new Line(\"l\").Says() + \"/\" + new Line(\"l\", 5, 1, 2).Says();"),
        ("a record's constructor ending in params", "return new Row(\"r\").Says() + \"/\" + new Row(\"r\", 5, 1, 2).Says();"),
        ("a method ending in params", "return Sums.Of(\"m\") + \"/\" + Sums.Of(\"m\", 5, 1, 2);"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AComponentsConstructorParameters_AreWhatCSharpPasses(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
