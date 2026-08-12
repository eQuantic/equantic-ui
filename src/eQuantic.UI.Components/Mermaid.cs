using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// Mermaid, drawn by the design system — no mermaid.js, no SVG document, no target-specific
/// renderer. The source parses into a graph, the graph solves into whole-dp geometry, and the
/// geometry is COMPONENTS: nodes are Boxes on the theme's surface tokens, the decision diamond is
/// a single-path <see cref="Vector"/> rhombus (the same vector door icons use on both targets),
/// edges are orthogonal hairline Boxes with vector arrowheads, labels are <see cref="Text"/>. One
/// tree, realized by the web realizer and by Photon — a diagram is finally just more UI.
/// <para>
/// The parser and the layout run on every side (C# for SSR and native, the transpiled twin in the
/// browser), and the layout keeps to integers and exact halves so both compilations place every
/// box on identical numbers.
/// </para>
/// <para>
/// GRACEFUL BY CONTRACT: a source whose grammar this subset does not speak renders as the fenced
/// code it came from — a chat answer with an exotic diagram degrades to something true, never to
/// a broken picture. v1 speaks <c>graph/flowchart TD|TB|LR</c> and <c>sequenceDiagram</c>;
/// dashed/thick links draw as the one line style, subgraphs and notes are skipped, gantt/class/
/// state/pie fall back to code.
/// </para>
/// </summary>
public sealed class Mermaid : StatelessComponent
{
    public Mermaid(string source)
    {
        Source = source;
    }

    public string Source { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var graph = MermaidParser.Parse(Source);
        if (graph == null)
            return new CodeBlock(Source, "mermaid") { ShowLineNumbers = false };

        var scene = MermaidLayout.Solve(graph);
        var canvas = new Stack { Width = scene.Width, Height = scene.Height };

        // Paint order: lines under heads under labels under nodes — a label chip sits ON its
        // edge, a node covers whatever routed beneath it.
        var ink = theme.BorderStrong;
        foreach (var segment in scene.Segments)
            canvas.Add(new Positioned(new Box(new BoxStyle
            {
                Width = segment.W,
                Height = segment.H,
                Background = ink,
            }), top: segment.Y, start: segment.X));

        foreach (var arrow in scene.Arrows)
            canvas.Add(PlaceArrowhead(arrow, ink));

        foreach (var label in scene.Labels)
        {
            // The SAME estimate the layout used to widen the gap this chip sits in.
            var width = MermaidLayout.LabelChipWidth(label.Text);
            canvas.Add(new Positioned(new Box(new BoxStyle
            {
                Padding = new EdgeInsets(6, 1, 6, 1),
                Background = theme.Background,
                BorderColor = theme.Border,
                BorderWidth = 1,
                CornerRadius = new CornerRadii(6),
            }, new Text(label.Text, TypeRole.Caption, theme.TextMuted, maxLines: 1)),
                top: label.Y - 10, start: label.X - width / 2));
        }

        foreach (var placed in scene.Nodes)
            canvas.Add(new Positioned(NodeView(placed, theme), top: placed.Y, start: placed.X));

        // The canvas is its own size; wide diagrams scroll sideways INSIDE the component, the
        // same containment contract every block on a page owes its column.
        return new ScrollView(canvas, ScrollAxis.Horizontal) { Width = SizeValue.Fill };
    }

    // ---- Nodes -------------------------------------------------------------------------------

    private static VisualNode NodeView(MermaidPlacedNode placed, IAppTheme theme)
    {
        var node = placed.Node;
        if (node.Shape == MermaidShape.Diamond) return DiamondView(placed, theme);

        var radius = node.Shape switch
        {
            MermaidShape.Circle => placed.H / 2,
            MermaidShape.Rounded => placed.H / 2,
            _ => 6,
        };

        return new Box(new BoxStyle
        {
            Width = placed.W,
            Height = placed.H,
            Background = theme.Surface,
            BorderColor = theme.BorderStrong,
            BorderWidth = 1,
            CornerRadius = new CornerRadii(radius),
            Padding = EdgeInsets.Symmetric(Space.S2, 0),
        }, new Text(node.Label, TypeRole.Label, theme.TextPrimary,
            maxLines: 2, align: TextAlignment.Center).Centered());
    }

    /// <summary>The decision rhombus: two stacked vector fills — the border-color rhombus with a
    /// surface-color one inset 3dp — and the label centered on top. Pure vector-door geometry, so
    /// Photon rasterizes exactly what the web draws.</summary>
    private static VisualNode DiamondView(MermaidPlacedNode placed, IAppTheme theme)
    {
        var side = placed.W;
        var rhombus = new IconGlyph("mermaidDiamond", "M50 0 L100 50 L50 100 L0 50 Z", ViewBox: "0 0 100 100");

        var stack = new Stack { Width = side, Height = side };
        stack.Add(new Vector(rhombus, side, theme.BorderStrong));
        stack.Add(new Positioned(new Vector(rhombus, side - 6, theme.Surface), top: 3, start: 3));
        stack.Add(new Box(new BoxStyle { Width = side, Height = side },
            new Text(placed.Node.Label, TypeRole.LabelSmall, theme.TextPrimary,
                maxLines: 2, align: TextAlignment.Center).Centered()));
        return stack;
    }

    // ---- Arrowheads ----------------------------------------------------------------------------

    private const float HeadSize = 9;

    // Glyphs are built INSIDE the methods, never in static initializers: a module-load-time
    // construction runs while the runtime's module cycle is still resolving on the client, and
    // the vocabulary class isn't a constructor yet. (The rule the runtime already lives by —
    // leaf modules construct lazily.)
    private static IconGlyph HeadGlyph(int direction)
    {
        if (direction == 1) return new IconGlyph("mermaidHeadRight", "M0 0 V100 L100 50 Z", ViewBox: "0 0 100 100");
        if (direction == 2) return new IconGlyph("mermaidHeadUp", "M50 0 L100 100 H0 Z", ViewBox: "0 0 100 100");
        if (direction == 3) return new IconGlyph("mermaidHeadLeft", "M100 0 V100 L0 50 Z", ViewBox: "0 0 100 100");
        return new IconGlyph("mermaidHeadDown", "M0 0 H100 L50 100 Z", ViewBox: "0 0 100 100");
    }

    /// <summary>Positions a head so its TIP touches the anchor point it was routed to.</summary>
    private static VisualNode PlaceArrowhead(MermaidArrowhead arrow, ColorToken ink)
    {
        var half = (HeadSize - 1) / 2;
        float top = arrow.Y - HeadSize;
        float start = arrow.X - half;
        if (arrow.Direction == 1)
        {
            top = arrow.Y - half;
            start = arrow.X - HeadSize;
        }
        else if (arrow.Direction == 2)
        {
            top = arrow.Y;
            start = arrow.X - half;
        }
        else if (arrow.Direction == 3)
        {
            top = arrow.Y - half;
            start = arrow.X;
        }
        return new Positioned(new Vector(HeadGlyph(arrow.Direction), HeadSize, ink), top: top, start: start);
    }
}
