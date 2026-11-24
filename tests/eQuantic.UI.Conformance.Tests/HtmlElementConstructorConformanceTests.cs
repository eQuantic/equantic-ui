using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A class over <c>HtmlElement</c>, the DOM escape hatch, that takes constructor parameters, built and read on both
/// sides, in TypeScript and in plain JavaScript (#1837). The primitive path kept a constructor parser of its own that
/// stored no constructor's syntax, so an explicit constructor's body never ran, a block's or an arrow's, and an
/// assignment to each parameter's namesake stood in its place. The parameters lost their defaults, and a primary
/// constructor became <c>constructor(props)</c> and lost its parameters.
/// <para>
/// Two more mechanisms of the same path are other issues, and these shapes keep clear of them: an expression-bodied
/// <c>Render()</c>, which is written as the fallback <c>div</c> (#1540), and an instance field's initializer (#1541).
/// Every <c>Render()</c> here is a block, and no field is initialized where it is declared.
/// </para>
/// </summary>
public class HtmlElementConstructorConformanceTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;
        using eQuantic.UI.Web;
        public sealed class Chip : HtmlElement
        {
            private readonly string _label;
            private readonly int _size;
            public Chip(string label, int size = 2) { _label = label; _size = size; }
            public string Says() => _label + "|" + _size;
            public override HtmlNode Render() { return new HtmlNode { Tag = "span", TextContent = _label + _size }; }
        }
        public sealed class Tag(string label, int size = 2) : HtmlElement
        {
            public string Says() => label + "|" + size;
            public override HtmlNode Render() { return new HtmlNode { Tag = "b", TextContent = label + size }; }
        }
        public sealed class Blocky : HtmlElement
        {
            private readonly string _text;
            public Blocky(string text) => _text = text;
            public override HtmlNode Render() { return new HtmlNode { Tag = "p", TextContent = _text }; }
        }
        public sealed class Clocked : HtmlElement
        {
            private readonly IClock? _clock;
            private readonly string _label;
            public Clocked(IClock? clock, string label) { _clock = clock; _label = label; }
            public string Says() => _label + "|" + (_clock == null ? "no clock" : "clock");
            public override HtmlNode Render() { return new HtmlNode { Tag = "i", TextContent = _label }; }
        }
        public sealed class Captioned : HtmlElement
        {
            public string Caption { get; set; }
            public int Count { get; }
            public Captioned(string caption, int count = 4) { Caption = caption.ToUpper(); Count = count; }
            public override HtmlNode Render() { return new HtmlNode { Tag = "em", TextContent = Caption + Count }; }
        }
        public sealed class Propped : HtmlElement
        {
            private readonly string _text;
            public Propped() { var props = "own"; _text = props; }
            public string Says() => _text;
            public override HtmlNode Render() { return new HtmlNode { Tag = "s", TextContent = _text }; }
        }
        public sealed class Plain : HtmlElement
        {
            public string Caption { get; set; } = "none";
            public override HtmlNode Render() { return new HtmlNode { Tag = "em", TextContent = Caption }; }
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("an explicit constructor, every argument passed", "return new Chip(\"a\", 5).Says();"),
        ("an explicit constructor, its default left out", "return new Chip(\"b\").Says();"),
        ("an explicit constructor beside an initializer", "var c = new Chip(\"c\", 7) { Title = \"t\" }; return c.Says() + \"|\" + c.Title;"),
        ("what an explicit constructor's fields render", "var n = new Chip(\"d\", 8).Render(); return n.Tag + \"|\" + n.TextContent;"),
        ("a primary constructor", "return new Tag(\"e\", 9).Says() + \"|\" + new Tag(\"f\").Render().TextContent;"),
        ("an arrow constructor", "var n = new Blocky(\"g\").Render(); return n.Tag + \"|\" + n.TextContent;"),
        ("an interface parameter first, passed as written", "return new Clocked(null, \"h\").Says();"),
        ("a constructor that writes properties, one of them read-only", "var n = new Captioned(\"i\").Render(); return n.TextContent + \"|\" + new Captioned(\"j\", 6).Count;"),
        ("a parameterless constructor whose body declares props", "return new Propped().Says();"),
        ("no constructor, a property's default and an initializer", "return new Plain().Render().TextContent + \"|\" + new Plain { Caption = \"set\" }.Render().TextContent;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnHtmlElementSubclass_KeepsWhatItsConstructorDoes(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
