using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// COMPONENT PARITY (docs/COVERAGE-PLAN.md slice 4). Everything the conformance suite executes on
/// both sides is language-level: an expression, a statement, a LINQ chain. A COMPONENT had only
/// pins — which compare the twin's SOURCE, not what it produces — and Studio walks, which run one
/// side. So a twin could be transpiled perfectly and still lower to a different tree, and nothing
/// would say so; that is how "a stateful component returned as a build's ROOT lost its state"
/// reached the tree with a green suite.
///
/// This pins the LOWERED tree — tag, attributes, event names, children — for a canonical set of
/// components, into a fixture the vitest twin replays through its own lowering. The attribute
/// values carry the atomic class names, so the style hash is pinned across the two sides here too.
/// Refresh with EQ_UPDATE_PARITY_FIXTURE=1.
/// </summary>
public class ComponentParityFixtureTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>The components under parity, by NAME. The twin spec builds the same names with the
    /// same arguments; a name present on one side only fails the guard on the other.</summary>
    private static IEnumerable<(string Name, VisualNode Node)> Cases() =>
    [
        ("text", new Text("hello", TypeRole.BodyM, Theme.TextPrimary)),
        ("button-primary", new Button("Save")),
        ("button-ghost-small", new Button("Cancel", Variant.Ghost, SizeVariant.Small)),
        ("switch-on", new Switch(true)),
        ("switch-off", new Switch(false)),
        ("progress-determinate", new ProgressBar(0.42f)),
        ("progress-indeterminate", new ProgressBar()),
        ("avatar", new Avatar("EM", SizeVariant.Large, "Edgar")),
        ("column-of-text", Stack(Space.S3, new Text("one", TypeRole.BodyM, Theme.TextPrimary),
                                            new Text("two", TypeRole.Label, Theme.TextPrimary))),
        ("button-in-column", Stack(Space.S2, new Button("A"), new Button("B", Variant.Outline))),
    ];

    private static VisualNode Stack(float gap, params VisualNode[] children)
    {
        var column = new Column(gap);
        foreach (var child in children) column.Add(child);
        return column;
    }

    [Fact]
    public void TheLoweredTrees_MatchTheSharedFixture()
    {
        var json = new JsonObject();
        foreach (var (name, node) in Cases())
        {
            // Through a STYLE SINK, which is the path SSR takes: declarations become atomic classes
            // instead of inline style. The client always atomizes, so lowering without a sink here
            // would compare two different representations of the same styling and call it a
            // divergence — and it would hide the one that matters, the class HASH agreeing.
            var sink = new StyleSink();
            json[name] = Canonical(WebRealizer.Lower(node, Theme, 1f, sink).Render());
        }

        var text = json.ToJson();
        var path = FixturePath();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_PARITY_FIXTURE") == "1")
        {
            File.WriteAllText(path, text);
            return;
        }

        File.ReadAllText(path).Replace("\r\n", "\n").Should().Be(text.Replace("\r\n", "\n"),
            "the lowered tree of every component must be the one the twin produces — regenerate "
            + "with EQ_UPDATE_PARITY_FIXTURE=1 once the twin agrees");
    }

    /// <summary>A lowered node as canonical JSON: attributes and event NAMES sorted, absent things
    /// absent. Events cross as names alone — a delegate and a closure cannot be compared, but
    /// whether the twin still HAS the handler is exactly what tends to go missing.</summary>
    private static JsonObject Canonical(HtmlNode node)
    {
        var attributes = new JsonObject();
        foreach (var pair in node.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal))
            if (pair.Value is not null) attributes[pair.Key] = JsonValue.Text(pair.Value);

        var result = new JsonObject { ["tag"] = JsonValue.Text(node.Tag) };
        if (node.Key is not null) result["key"] = JsonValue.Text(node.Key);
        if (node.TextContent is not null) result["text"] = JsonValue.Text(node.TextContent);
        result["attrs"] = attributes;
        result["events"] = JsonValue.List(node.Events.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Select(JsonValue.Text));
        result["children"] = JsonValue.List(node.Children.Select(Canonical));
        return result;
    }

    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared",
            "component-parity.fixture.json");
    }

    /// <summary>The smallest JSON writer that produces exactly what JSON.stringify(x, null, 2)
    /// produces — the fixture has to be diffable and byte-comparable from both sides.</summary>
    private abstract class JsonValue
    {
        public static JsonValue Text(string value) => new JsonText(value);
        public static JsonValue List(IEnumerable<JsonValue> items) => new JsonList(items.ToList());
        public abstract void Write(StringBuilder builder, int indent);

        public string ToJson()
        {
            var builder = new StringBuilder();
            Write(builder, 0);
            builder.Append('\n');
            return builder.ToString();
        }

        protected static void Newline(StringBuilder builder, int indent) =>
            builder.Append('\n').Append(' ', indent * 2);
    }

    private sealed class JsonText(string value) : JsonValue
    {
        public override void Write(StringBuilder builder, int indent) =>
            builder.Append(JsonSerializer.Serialize(value));
    }

    private sealed class JsonList(List<JsonValue> items) : JsonValue
    {
        public override void Write(StringBuilder builder, int indent)
        {
            if (items.Count == 0) { builder.Append("[]"); return; }
            builder.Append('[');
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) builder.Append(',');
                Newline(builder, indent + 1);
                items[i].Write(builder, indent + 1);
            }
            Newline(builder, indent);
            builder.Append(']');
        }
    }

    private sealed class JsonObject : JsonValue
    {
        private readonly List<(string Key, JsonValue Value)> _entries = [];
        public JsonValue this[string key] { set => _entries.Add((key, value)); }

        public override void Write(StringBuilder builder, int indent)
        {
            if (_entries.Count == 0) { builder.Append("{}"); return; }
            builder.Append('{');
            for (var i = 0; i < _entries.Count; i++)
            {
                if (i > 0) builder.Append(',');
                Newline(builder, indent + 1);
                builder.Append(JsonSerializer.Serialize(_entries[i].Key)).Append(": ");
                _entries[i].Value.Write(builder, indent + 1);
            }
            Newline(builder, indent);
            builder.Append('}');
        }
    }
}
