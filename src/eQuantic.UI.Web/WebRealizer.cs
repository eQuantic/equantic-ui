using System.Linq;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The WEB REALIZER for the shared abstract vocabulary (docs/SHARED-COMPONENTS-PLAN.md): lowers a
/// <see cref="VisualNode"/> tree to <see cref="HtmlElement"/>s — the same trees the native realizer
/// lowers to Photon pixels. Colors lower as <c>light-dark()</c> values straight from the tokens, so
/// the produced DOM is MODE-FREE like the abstract tree (theme switching = <c>color-scheme</c>).
/// This is the server-side (SSR) lowering; the TypeScript runtime mirrors these exact rules
/// client-side, and hydration correctness depends on the two staying identical — every mapping rule
/// here is therefore normative.
///
/// Layout parity notes (v1): flex maps 1:1 (direction/gap/justify/align, Flexible → <c>flex: n s basis</c>
/// matching the native leftover-by-weight semantics); Photon's inside border maps to
/// <c>box-sizing: border-box</c>; the cross-target layout harness tightens the remainder (plan).
/// </summary>
public static class WebRealizer
{
    /// <summary>
    /// Lower with ATOMIC style emission (docs/STYLE-SEMANTICS-PLAN.md §2): when a
    /// <paramref name="styles"/> sink is given, every element's style object is converted into
    /// deduplicated atomic classes collected into the sink — the markup carries class names and the
    /// sink carries the (once-per-declaration) rules. Without a sink, styles stay inline (tests and
    /// standalone lowering keep the direct form).
    /// <para>
    /// ONE method, where there were two: the shorter one existed only to default the sink away, and
    /// an optional parameter says that in the signature. Every call binds unchanged — the
    /// overload it replaces took the same arguments in the same order.
    /// </para>
    /// </summary>
    public static HtmlElement Lower(
        VisualNode node, IAppTheme theme, float typeScale = 1f, StyleSink? styles = null)
    {
        var context = new ComponentContext(theme, typeScale);
        var root = LowerRoot(node, context)
               ?? new RealizedElement("span"); // layout-only nodes outside a flex row lower to nothing
        if (styles != null)
            StyleAtomizer.AtomizeTree(root, ThemeVarMap.For(theme), styles);
        return root;
    }

    /// <summary>
    /// The pass's visitor, built once per lowering: what each word of the vocabulary becomes is
    /// <see cref="WebLoweringVisitor"/>'s, and this class is the entry point plus the atomizing
    /// step that wraps it.
    /// </summary>
    private static HtmlElement? LowerRoot(VisualNode node, ComponentContext context) =>
        new WebLoweringVisitor(context).Lower(node, horizontalAxis: null);

    internal static string PaintCss(VectorPaint paint) => paint.Kind switch
    {
        VectorPaintKind.Inherit => "currentColor",
        VectorPaintKind.Solid => CssColor(paint.Color),
        VectorPaintKind.LinearGradient or VectorPaintKind.RadialGradient when paint.Gradient is { } run
            => $"url(#{GradientId(run)})",
        _ => "none",
    };
    /// <summary>
    /// The runs a drawing paints with, keyed by the id the shapes will name. Ordered by first use
    /// so the markup reads in the order the artwork does, and deduplicated because the id IS the
    /// content: two shapes with the same run declare it once.
    /// </summary>
    internal static Dictionary<string, VectorPaint> Gradients(VectorDrawing artwork)
    {
        var runs = new Dictionary<string, VectorPaint>(StringComparer.Ordinal);
        foreach (var shape in artwork.Shapes)
        {
            foreach (var paint in (VectorPaint[])[shape.Fill, shape.Stroke])
            {
                if (!paint.IsGradient) continue;
                runs.TryAdd(GradientId(paint.Gradient!.Value), paint);
            }
        }
        return runs;
    }
    /// <summary>
    /// A gradient's id: the FNV hash of everything that makes it what it is. Content-addressed
    /// rather than counted, for the reason every other generated id here is — a counter depends on
    /// how many drawings came before, and SSR and the client twin do not agree about that.
    /// <para>
    /// PUBLIC because it is a promise to the other producer rather than an implementation detail:
    /// the client twin computes the same string from the same run, and a cross-pin fixture holds
    /// the two to it.
    /// </para>
    /// </summary>
    public static string GradientId(VectorGradient run)
    {
        var text = new System.Text.StringBuilder();
        text.Append(run.UserSpace ? 'u' : 'o').Append('|')
            .Append(TokenCss.Number(run.X1)).Append(',').Append(TokenCss.Number(run.Y1)).Append(',')
            .Append(TokenCss.Number(run.X2)).Append(',').Append(TokenCss.Number(run.Y2)).Append(',')
            .Append(TokenCss.Number(run.Radius));
        foreach (var stop in run.Stops)
            text.Append('|').Append(TokenCss.Number(stop.Offset)).Append(':').Append(CssColor(stop.Color));
        return $"eq-g-{StyleAtomizer.Hash(text.ToString())}";
    }
    /// <summary>
    /// Every run the DOCUMENT paints with, in one container the whole page references. See
    /// <see cref="GradientSink"/> for why it stopped living inside each drawing.
    /// </summary>
    internal static HtmlElement GradientContainer(IReadOnlyDictionary<string, VectorPaint> runs)
    {
        var svg = new RealizedElement("svg")
        {
            Id = GradientSink.ContainerId,
            Style = new HtmlStyle
            {
                // Out of flow and zero-sized — NOT display:none, which is what makes a paint server
                // unusable and is the bug this replaces.
                Position = Position.Absolute,
                Width = "0",
                Height = "0",
                Overflow = "hidden",
            },
            RawAttributes = new Dictionary<string, string> { ["aria-hidden"] = "true" },
        };

        var defs = new RealizedElement("defs");
        foreach (var run in runs) defs.Children.Add(GradientElement(run.Key, run.Value));
        svg.Children.Add(defs);
        return svg;
    }
    /// <summary>
    /// One paint server, in SVG's own words. The stop's alpha rides <c>stop-opacity</c> rather than
    /// an 8-digit colour: that is the spelling every renderer agrees on, and the one a designer sees
    /// when they open the file again.
    /// </summary>
    internal static HtmlElement GradientElement(string id, VectorPaint paint)
    {
        var run = paint.Gradient!.Value;
        var radial = paint.Kind == VectorPaintKind.RadialGradient;
        var element = new RealizedElement(radial ? "radialGradient" : "linearGradient");
        var attributes = new Dictionary<string, string> { ["id"] = id };
        if (radial)
        {
            attributes["cx"] = TokenCss.Number(run.X1);
            attributes["cy"] = TokenCss.Number(run.Y1);
            attributes["r"] = TokenCss.Number(run.Radius);
        }
        else
        {
            attributes["x1"] = TokenCss.Number(run.X1);
            attributes["y1"] = TokenCss.Number(run.Y1);
            attributes["x2"] = TokenCss.Number(run.X2);
            attributes["y2"] = TokenCss.Number(run.Y2);
        }
        // The default is objectBoundingBox and SVG's own, so only the other one is written.
        if (run.UserSpace) attributes["gradientUnits"] = "userSpaceOnUse";
        element.RawAttributes = attributes;

        foreach (var stop in run.Stops)
        {
            var child = new RealizedElement("stop");
            var stopAttributes = new Dictionary<string, string>
            {
                ["offset"] = TokenCss.Number(stop.Offset),
                ["stop-color"] = CssColor(Primitives.Color.FromRgb(stop.Color.R, stop.Color.G, stop.Color.B)),
            };
            if (stop.Color.A < 255) stopAttributes["stop-opacity"] = TokenCss.Number(stop.Color.A / 255f);
            child.RawAttributes = stopAttributes;
            element.Children.Add(child);
        }
        return element;
    }

    /// <summary>
    /// A literal artwork colour, which is NOT a token: it is what the designer drew, and it does
    /// not move with the theme. Spelled `#rrggbb(aa)` because the client twin's own formatter is —
    /// the two lowerings have to agree byte for byte or hydration patches every path it touches.
    /// </summary>
    internal static string CssColor(Primitives.Color color) =>
        color.A == 255
            ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
            : $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";
}
