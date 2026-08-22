using System.Runtime.CompilerServices;
using System.Text;
using System.Globalization;
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

    /// <summary>
    /// The components under parity, by NAME, each with the PRESSES that drive it. The twin spec
    /// builds the same names with the same arguments, and a name present on one side only fails
    /// the guard on the other.
    /// </summary>
    /// <remarks>
    /// A press is the index of a click handler in the lowered tree, in document order, invoked
    /// between one frame and the next. A component with no presses is one frame — its lowering —
    /// and one with presses is the lowering after each of them, which is where a twin whose state
    /// does not move shows up.
    /// </remarks>
    private static IEnumerable<(string Name, VisualNode Node, int[] Presses)> Cases() =>
    [
        ("text", new Text("hello", TypeRole.BodyM, Theme.TextPrimary), NoPresses),
        ("button-primary", new Button("Save"), NoPresses),
        ("button-ghost-small", new Button("Cancel", Variant.Ghost, SizeVariant.Small), NoPresses),
        ("switch-on", new Switch(true), NoPresses),
        ("switch-off", new Switch(false), NoPresses),
        ("progress-determinate", new ProgressBar(0.42f), NoPresses),
        ("progress-indeterminate", new ProgressBar(), NoPresses),
        ("avatar", new Avatar("EM", SizeVariant.Large, "Edgar"), NoPresses),
        ("column-of-text", Stack(Space.S3, new Text("one", TypeRole.BodyM, Theme.TextPrimary),
                                           new Text("two", TypeRole.Label, Theme.TextPrimary)), NoPresses),
        ("button-in-column", Stack(Space.S2, new Button("A"), new Button("B", Variant.Outline)), NoPresses),

        // A BROAD sweep of the library. Every one of these has a twin whose constructor mirrors
        // the C# one parameter for parameter, so the same arguments build the same component on
        // both sides — which is what makes a difference in the lowered tree mean something.
        ("badge", new Badge(7), NoPresses),
        ("badge-overflow", new Badge(140, 99, Variant.Primary), NoPresses),
        ("card", new Card(new Text("body", TypeRole.BodyM, Theme.TextPrimary)), NoPresses),
        ("checkbox-on", new Checkbox(true, null, "Accept"), NoPresses),
        ("checkbox-off", new Checkbox(false), NoPresses),
        ("chip", new Chip("Filter"), NoPresses),
        ("chip-selected", new Chip("Chosen", ChipKind.Filter, true), NoPresses),
        ("divider", new Divider(), NoPresses),
        ("divider-vertical", new Divider(DividerInset.None, DividerAxis.Vertical), NoPresses),
        ("banner", new Banner(Variant.Destructive, "Careful", "Something needs attention"), NoPresses),
        ("stepper", new Stepper(3), NoPresses),
        ("stepper-labelled", new Stepper(3) { Label = "quantity" }, NoPresses),
        ("pagination", new Pagination(5, 2), NoPresses),
        ("page-indicator", new PageIndicator(4, 1), NoPresses),
        ("tooltip", new Tooltip(new Text("hover", TypeRole.BodyM, Theme.TextPrimary), "the tip"), NoPresses),
        ("tabs", new Tabs(["One", "Two", "Three"], 1), NoPresses),
        ("search-field", new SearchField("term"), NoPresses),
        ("text-input", new TextInput("value", null, "Label"), NoPresses),
        ("radio-group", new RadioGroup(["a", "b"], 0), NoPresses),
        ("empty-state", new EmptyState(Icons.Search, "Nothing here", "Try another term"), NoPresses),

        // DRIVEN: the state has to move the same way on both sides, and one lowering cannot show
        // that. Opening a Select and switching an Accordion's open section are the two smallest
        // changes of state that rewrite a subtree.
        ("select-opens", new Select(["alpha", "beta", "gamma"], 0), [0]),
        ("accordion-switches", new Accordion([
            new AccordionItem("One") { Content = new Text("body one", TypeRole.BodyM, Theme.TextPrimary) },
            new AccordionItem("Two") { Content = new Text("body two", TypeRole.BodyM, Theme.TextPrimary) },
        ], 0), [1]),

        // A COMPOSITE, and the first case here whose tree is a grid: 31 cells, seven column
        // headers, a roving tab order, and a selection stated as an attribute. Pinned under a
        // fixed culture and a fixed date, or the fixture would say something different every day.
        ("calendar-july-2026", new eQuantic.UI.Components.Calendar(new DateOnly(2026, 7, 17)), NoPresses),
        ("calendar-bounded", new eQuantic.UI.Components.Calendar(new DateOnly(2026, 7, 17),
            min: new DateOnly(2026, 7, 10), max: new DateOnly(2026, 7, 20)), NoPresses),
        // DRIVEN: pressing a day is the whole point of a calendar, and the frame after it is where
        // a twin whose state moved differently shows. Index 2 is the first day CELL — the two
        // chevrons come first in tree order — so this picks July 1st over the 17th.
        ("calendar-picks-a-day", new eQuantic.UI.Components.Calendar(new DateOnly(2026, 7, 17)), [2]),
    ];

    /// <summary>A component that is only lowered, never driven.</summary>
    private static readonly int[] NoPresses = [];

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
        // A FIXED culture for the whole set: a calendar reads CultureInfo for its day and month
        // names, so a fixture generated in São Paulo would differ from one generated in Berlin.
        // The UI culture too, and for a different reason: a component's own labels come from
        // SdkStrings through a ResourceManager, which reads THAT one. The twin installs the same
        // pair before replaying.
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        try
        {
            foreach (var (name, node, presses) in Cases())
                json[name] = JsonValue.List(Frames(node, presses).Select(Canonical));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
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

    /// <summary>Every frame of a case: the lowering, then the lowering after each press. Lowering
    /// goes through a STYLE SINK, which is the path SSR takes — declarations become atomic classes
    /// instead of inline style, and the client always atomizes, so lowering without one would
    /// compare two representations of the same styling and call it a divergence, while hiding the
    /// thing that matters, which is the class HASH agreeing.</summary>
    private static IEnumerable<HtmlNode> Frames(VisualNode node, int[] presses)
    {
        var frame = WebRealizer.Lower(node, Theme, 1f, new StyleSink()).Render();
        ReferencesResolve(frame);
        yield return frame;
        foreach (var index in presses)
        {
            var handlers = ClickHandlers(frame).ToList();
            handlers.Count.Should().BeGreaterThan(index,
                "a case presses the click handler at an index the lowered tree has");
            // A click handler IS an Action (HtmlElement.OnClick, Pressable.OnPressed), so call it.
            // DynamicInvoke stays as the fallback for a shape nobody has emitted yet, and the cast
            // failing is worth more than reflection quietly accepting anything.
            if (handlers[index] is Action press) press();
            else handlers[index].DynamicInvoke();
            frame = WebRealizer.Lower(node, Theme, 1f, new StyleSink()).Render();
            ReferencesResolve(frame);
            yield return frame;
        }
    }

    /// <summary>
    /// A generated ID reduced to its shape. An anchored panel's id is a HASH, and the two sides
    /// hash different inputs — safe today only because an open panel never comes from SSR, so the
    /// two never meet in one document. Comparing the hashes would fail on a difference the product
    /// allows; NOT comparing them would hide a reference that points at nothing, so
    /// <see cref="ReferencesResolve"/> checks that separately, on the un-normalised tree.
    /// </summary>
    private static string Normalize(string attribute, string value)
    {
        // CLASS is compared as a SET. Attribute order does not enter the CSS cascade — the
        // stylesheet's does — so a class list in another order is the same styling, and the two
        // sides do build it in another order (C# puts `eq-anchor-scrim` before the atomised
        // declarations; the twin appends it). What carries meaning is WHICH classes are there,
        // because each one is a hash of a declaration, and that is pinned.
        if (attribute == "class")
            value = string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .OrderBy(name => name, StringComparer.Ordinal));
        return System.Text.RegularExpressions.Regex.Replace(value, @"eq-panel-[a-z0-9]+", "eq-panel-#");
    }

    /// <summary>Every ARIA reference in the tree points at an id the tree HAS. A dangling
    /// `aria-activedescendant` reads to a screen reader as no focus at all, and the attribute pins
    /// it against itself, so nothing that compares attribute values can see it.</summary>
    private static void ReferencesResolve(HtmlNode root)
    {
        var ids = new HashSet<string>();
        var references = new List<(string Attribute, string Value)>();
        Walk(root);
        foreach (var (attribute, value) in references)
            foreach (var target in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                ids.Should().Contain(target, $"{attribute} must name an element the tree has");
        return;

        void Walk(HtmlNode node)
        {
            if (node.Attributes.TryGetValue("id", out var id) && id is not null) ids.Add(id);
            foreach (var name in new[] { "aria-activedescendant", "aria-controls", "aria-labelledby",
                         "aria-describedby", "aria-owns" })
                if (node.Attributes.TryGetValue(name, out var value) && value is not null)
                    references.Add((name, value));
            foreach (var child in node.Children) Walk(child);
        }
    }

    /// <summary>Every click handler in the tree, in DOCUMENT order — the order the twin walks too,
    /// so an index means the same control on both sides.</summary>
    private static IEnumerable<Delegate> ClickHandlers(HtmlNode node)
    {
        foreach (var pair in node.Events.Where(e => e.Key is "click" or "onclick")) yield return pair.Value;
        foreach (var child in node.Children)
            foreach (var handler in ClickHandlers(child)) yield return handler;
    }

    /// <summary>A lowered node as canonical JSON: attributes and event NAMES sorted, absent things
    /// absent. Events cross as names alone — a delegate and a closure cannot be compared, but
    /// whether the twin still HAS the handler is exactly what tends to go missing.</summary>
    private static JsonObject Canonical(HtmlNode node)
    {
        var attributes = new JsonObject();
        foreach (var pair in node.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal))
            if (pair.Value is not null)
                attributes[pair.Key] = JsonValue.Text(Normalize(pair.Key, pair.Value));

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

    /// <summary>
    /// The smallest JSON writer that gives the fixture a STABLE, diffable shape: two-space indent,
    /// entries in insertion order, one trailing newline. It is byte-compared against the committed
    /// file by this side only — the twin imports the fixture as parsed JSON, so escaping choices
    /// (System.Text.Json escapes more than JSON.stringify does) change how it READS, never whether
    /// the two sides agree.
    /// </summary>
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

        /// <summary>Appends, and REFUSES a repeat: two entries under one key is duplicate keys in
        /// the output, which is not canonical and which a reader would never see. For the case
        /// names it means a copy-pasted name fails here instead of quietly covering nine
        /// components while claiming ten.</summary>
        public JsonValue this[string key]
        {
            set
            {
                if (_entries.Any(entry => entry.Key == key))
                    throw new InvalidOperationException($"duplicate key '{key}' in the parity fixture");
                _entries.Add((key, value));
            }
        }

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
