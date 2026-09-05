using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

using eQuantic.Console;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// The <see cref="Markdown"/> component, live: one source string, rendered entirely by the design
/// system — headings on the type scale, fenced code through CodeBlock's own highlighter, tables as
/// grids, all in the app's theme. The segmented control swaps the SOURCE in client state, which
/// makes the browser re-parse and re-render through the transpiled twin — the same parser that
/// produced the SSR you first landed on.
/// </summary>
[Page("/markdown", Title = "Markdown — eQuantic Console")]
public sealed class MarkdownScreen : StatefulComponent
{
    private int _document;

    private static readonly string Doc = string.Join("\n",
    [
        "# Markdown, write-once",
        "",
        "One `Markdown(source)` and the document below renders through the **design system** —",
        "the same components, the same theme, on web and on Photon.",
        "",
        "## What rides the type scale",
        "",
        "- Headings map to `TypeRole` rungs, overridable per level via *MarkdownStyle*",
        "- Prose is ONE `Text` with spans, so a sentence with `code` in it wraps between **words**",
        "- Links ride inside the sentence: the [component library](/declarative) is one",
        "",
        "> A quote keeps the design's start border, and callouts stay an app-level dialect.",
        "",
        "```csharp",
        "var page = Markdown(source);   // themed by the app's own IAppTheme",
        "```",
        "",
        "| Block | Lowers to |",
        "| --- | --- |",
        "| Fenced code | `CodeBlock` (highlighted) |",
        "| Rule | `Divider` |",
        "| Table | `Grid` rows |",
        "",
        "---",
        "",
        "Switch the document above: the browser re-parses through the **transpiled twin**,",
        "no server round-trip.",
    ]);

    private static readonly string Diagrams = string.Join("\n",
    [
        "# Mermaid, natively",
        "",
        "The fences below are ```mermaid — parsed and DRAWN by the SDK itself: nodes are Boxes on",
        "the theme's tokens, the decision diamond is a single-path Vector, and each edge is one",
        "*stroked curve* in a box of its own aspect. No mermaid.js — the same tree Photon renders.",
        "",
        "```mermaid",
        "graph TD",
        "  A[Charge created] --> B{Risk check}",
        "  B -->|clear| C(Capture)",
        "  B -->|flagged| D[Manual review]",
        "  D --> A",
        "  C --> E((Settled))",
        "```",
        "",
        "The wiki's own architecture diagram — left-to-right, labeled edges:",
        "",
        "```mermaid",
        "graph LR",
        "  A[C# components] --> B{eqc}",
        "  B -->|static shell| C[SSR HTML]",
        "  B -->|dynamic logic| D(TypeScript)",
        "  C --> E{Embedded Bun}",
        "  D --> E",
        "  E --> F[wwwroot/_equantic]",
        "```",
        "",
        "And a sequence, laid out from declaration order alone:",
        "",
        "```mermaid",
        "sequenceDiagram",
        "  participant U as Checkout",
        "  participant G as Gateway",
        "  U->>G: authorize(card)",
        "  G-->>U: token",
        "  G->>G: audit trail",
        "```",
        "",
        "A grammar outside the subset falls back to the fence as code:",
        "",
        "```mermaid",
        "gantt",
        "  title Not yet spoken",
        "```",
    ]);

    private static readonly string Chat = string.Join("\n",
    [
        "Here's the plan for the payment retry queue:",
        "",
        "1. Read the failed charge from `payments.retries`",
        "2. Recompute the fee with **the current schedule**, not the one at failure time",
        "3. Ask the gateway for a fresh token",
        "",
        "```json",
        "{ \"retry\": true, \"backoff\": \"PT15M\" }",
        "```",
        "",
        "The important part: *never* retry a `card_declined` — that answer is final.",
    ]);

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    /// <summary>
    /// The frame, then this screen inside it. Every route wraps itself: there is no separate
    /// layout, and none is needed — the reconciler matches the frame position for position across
    /// a navigation, so the sidebar is never rebuilt and only the middle changes.
    /// </summary>
    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/markdown", "Markdown", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;

        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        column.Add(new Text("Markdown", TypeRole.Heading, theme.TextPrimary, maxLines: 1));
        column.Add(new Text("A document, a chat answer — same component, the app's theme.",
            TypeRole.BodyM, theme.TextMuted, maxLines: 2));
        column.Add(new SegmentedControl(["Document", "Chat answer", "Diagrams"], _document,
            i => SetState(() => _document = i))
        { Size = SizeVariant.Small, Stretch = false });

        column.Add(new Card(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S5),
        }, new Markdown(_document == 0 ? Doc : _document == 1 ? Chat : Diagrams)
        {
            Style = new MarkdownStyle { CodeLineNumbers = _document == 0 },
        }))
        { Width = SizeValue.Fill });

        var page = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S6),
        }, column);

        return new ScrollView(page);
    }
}
