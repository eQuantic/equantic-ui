using System.Linq;

using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Graphics — the words that draw rather than place. Each lowers to an element the browser paints
/// from data the server already holds: an <c>&lt;svg&gt;</c> of paths, an <c>&lt;img&gt;</c>, a
/// canvas the client takes over. <c>LowerGlyph</c> is shared between two of them, because a vector
/// IS an icon free of the §07 size whitelist and of the square box.
/// </summary>
internal sealed partial class WebLoweringVisitor
{
    /// <summary>
    /// Spec B15, drawn inside the fence: an SVG of 8 rrect bars (2×5 in the 16 viewBox) rotated
    /// i·45° about the center; the phase stagger rides per-bar NEGATIVE animation-delays over the
    /// generated 800ms 1→0.3 fade (exact parity with the native f(t) alphas). Color inherits via
    /// currentColor exactly like Icon; the 400ms anti-flash appear delay and the Reduce Motion
    /// pulse-in-place (delays zeroed) live in the generated stylesheet.
    /// </summary>
    private HtmlElement LowerSpinner(Spinner spinner)
    {
        var svg = new RealizedElement("svg")
        {
            ClassName = "eq-spinner",
            Style = new HtmlStyle
            {
                Width = TokenCss.Px(spinner.Size),
                Height = TokenCss.Px(spinner.Size),
                Color = spinner.Color is { } tint ? TokenCss.Value(tint) : null,
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["viewBox"] = "0 0 16 16",
                ["fill"] = "currentColor",
                ["aria-hidden"] = "true",
            },
        };

        var step = Spinner.RevolutionMs / 8;
        for (var i = 0; i < 8; i++)
        {
            var bar = new RealizedElement("rect")
            {
                Style = new HtmlStyle { AnimationDelay = $"-{i * step}ms" },
                RawAttributes = new Dictionary<string, string>
                {
                    ["x"] = "7",
                    ["y"] = "0",
                    ["width"] = "2",
                    ["height"] = "5",
                    ["rx"] = "1",
                    ["transform"] = $"rotate({i * 45} 8 8)",
                },
            };
            svg.Children.Add(bar);
        }
        return svg;
    }

    /// <summary>
    /// W3 lowering: the app's own drawing as one inline <c>&lt;svg&gt;</c> whose children are the
    /// shapes it painted, in call order.
    /// <para>
    /// SVG rather than <c>&lt;canvas&gt;</c> for the reasons the rest of this realizer chose it: it
    /// SSRs to the same pixels it hydrates to, it scales with the device pixel ratio for free, and
    /// the reconciler diffs it like any other markup. A <c>&lt;canvas&gt;</c> would be a blank
    /// rectangle on the server and a second rendering model to keep in step.
    /// </para>
    /// <para>
    /// The pointer handlers are MARKED here (<c>data-eq-canvas</c>) and wired by the runtime, which
    /// is the same contract Shortcut uses: SSR has no pointer events, so the marker is the whole
    /// server-side story and the hydrated DOM is identical.
    /// </para>
    /// </summary>
    private HtmlElement LowerCanvas(Canvas canvas)
    {
        var width = canvas.Width.Kind == SizeKind.Fixed ? canvas.Width.Value : 0;
        var height = canvas.Height.Kind == SizeKind.Fixed ? canvas.Height.Value : 0;
        var measured = width > 0 && height > 0;

        // A FIXED canvas knows its box here, so SSR carries the finished picture. A FILLING one
        // does not — CSS decides its box after this markup exists — so the server emits the shell
        // and the runtime draws it once the element has been measured (canvas-surface.ts), then
        // again on every resize. Photon needs none of this: it lays out and paints every frame, so
        // the painter there always answers with the real box. Drawing here at zero instead would
        // put every `Width / 2` in the top-left corner and leave it there.
        var painter = new SvgCanvasPainter(width, height);
        if (measured) canvas.Draw(painter);

        var svg = new RealizedElement("svg")
        {
            Style = new HtmlStyle
            {
                // Block for the same reason every other inline svg here is: an inline one would
                // sit on the text baseline of whatever box holds it.
                Display = Display.Block,
                Width = canvas.Width.Kind == SizeKind.Fixed ? TokenCss.Px(width) : "100%",
                Height = canvas.Height.Kind == SizeKind.Fixed ? TokenCss.Px(height) : "100%",
            },
            RawAttributes = new Dictionary<string, string>(),
        };
        // A viewBox only when the box is known: with a filling canvas the app draws in the SAME
        // coordinates the browser lays out, so no scaling should happen at all.
        if (measured)
            svg.RawAttributes["viewBox"] = $"0 0 {TokenCss.Number(width)} {TokenCss.Number(height)}";

        if (canvas.Label is { Length: > 0 } label)
        {
            svg.RawAttributes["role"] = "img";
            svg.RawAttributes["aria-label"] = label;
        }
        else svg.RawAttributes["aria-hidden"] = "true";

        var interactive = canvas.OnPointerDown is not null || canvas.OnPointerMove is not null
            || canvas.OnPointerUp is not null || canvas.OnPointerLeave is not null;
        // NO marker from the server, and that is deliberate rather than an omission. The runtime's
        // surface controller looks a declaration up BY PATH, and the path is a client-side identity
        // this realizer does not have — emitting a placeholder instead would be worse than emitting
        // nothing, because the reconciler adds a missing data attribute but does not overwrite one
        // that is already there, so the placeholder would stick and the canvas would never paint.
        // The client marks it on hydration, exactly as InView does for the same reason.

        if (interactive)
        {
            // DataAttributes is nullable and starts null — indexing it would have thrown on the
            // first interactive canvas anyone wrote.
            svg.DataAttributes ??= new Dictionary<string, string>();
            svg.DataAttributes["eq-canvas"] = "1";
            // And it TAKES the pointer, explicitly: `pointer-events` inherits, and the layers of a
            // Stack switch it off so the picture on top never blocks the control beneath — which
            // silenced a chart's own canvas inside its plot Stack: the hover fell through to the
            // card behind it and the tooltip never came. On Photon the canvas registers its own
            // region; this is the same statement in the DOM's language (see lowering.ts).
            svg.Style.PointerEvents = "auto";
        }
        else
        {
            // A DECORATIVE canvas must not swallow the press that belongs to what is under it —
            // Photon's hit-testing skips a canvas with no handlers (it registers no region at all),
            // and the DOM has no such rule: a filling svg over a Stack would eat every click. This
            // is the line that makes the two targets behave the same.
            svg.Style.PointerEvents = "none";
        }

        foreach (var shape in painter.Shapes) svg.Children.Add(shape);
        return svg;
    }

    /// <summary>A vector shape lowers exactly like an icon — the differences are that its size is
    /// the author's rather than the §07 whitelist's, and that its box need not be square.</summary>
    private HtmlElement LowerVector(Vector vector) =>
        LowerGlyph(vector.Glyph, vector.Size, vector.Height, vector.Color, vector.Label);

    /// <summary>
    /// Spec A10 lowering: inline 24×24-viewBox SVG with the registry's single alpha-mask path and
    /// <c>fill="currentColor"</c> — the tint rides the CSS <c>color</c> property exactly like text
    /// (token → light-dark()). Null color inherits; null label = decorative (aria-hidden).
    /// </summary>
    private HtmlElement LowerIcon(Icon icon)
        => LowerGlyph(icon.Glyph, icon.Size, icon.Size, icon.Color, icon.Label);

    /// <summary>
    /// Artwork stays VECTOR in the DOM: one inline <c>&lt;svg&gt;</c> carrying the drawing's own
    /// viewBox, and one <c>&lt;path&gt;</c> per shape with the paint the file chose. It scales
    /// without blurring, it prints, and it costs no request.
    /// <para>
    /// The shapes the file left as <c>currentColor</c> are emitted as <c>currentColor</c> — the
    /// word, not a resolved value — so the CSS colour on the wrapper answers them. That is what
    /// makes a monochrome mark follow the theme, and it is why the tint is set as `color` rather
    /// than painted into every path.
    /// </para>
    /// </summary>
    private HtmlElement LowerDrawing(Drawing drawing)
    {
        var svg = new RealizedElement("svg")
        {
            Style = new HtmlStyle
            {
                // Block for the same reason a glyph is: an inline svg sits on the text baseline of
                // whatever holds it and picks up a line box it never asked for.
                Display = Display.Block,
                Width = TokenCss.Px(drawing.Width),
                Height = TokenCss.Px(drawing.Height),
                Color = drawing.Tint is { } tint ? TokenCss.Value(tint) : null,
            },
        };
        svg.RawAttributes = new Dictionary<string, string>
        {
            ["viewBox"] = drawing.Artwork.ViewBox,
            // Each axis scales independently, matching the box the author asked for — the same
            // contract Vector states. A caller who wants the artwork undistorted omits the height
            // and gets the drawing's own aspect.
            ["preserveAspectRatio"] = "none",
            ["fill"] = "none",
        };
        if (drawing.Label is { } label) svg.RawAttributes["aria-label"] = label;
        else svg.RawAttributes["aria-hidden"] = "true";

        // Every run the artwork paints with, declared once and referenced by the shapes. The id is
        // the hash of the gradient ITSELF, so two shapes sharing a run share a def.
        //
        // WHERE that def goes is the document's business, not this drawing's. Inline, a second
        // drawing with the same run declares the same id again, and `url(#id)` binds to the first in
        // DOCUMENT order — which, once an AdaptiveNode puts every arm in the page, is the arm the
        // media query hides. A paint server in a display:none subtree is not rendered, so the shape
        // came out unpainted on the layout it was meant for. With a sink armed the runs go to the
        // page's one container instead; without one — a drawing realized alone, a unit test — they
        // stay inline, because there is no document to put a container in.
        if (GradientSink.Ambient is { } sink)
        {
            foreach (var gradient in WebRealizer.Gradients(drawing.Artwork))
                sink.Add(gradient.Key, gradient.Value);
        }
        else
        {
            var defs = new RealizedElement("defs");
            foreach (var gradient in WebRealizer.Gradients(drawing.Artwork))
                defs.Children.Add(WebRealizer.GradientElement(gradient.Key, gradient.Value));
            if (defs.Children.Count > 0) svg.Children.Add(defs);
        }

        foreach (var shape in drawing.Artwork.Shapes)
        {
            var path = new RealizedElement("path");
            var attributes = new Dictionary<string, string> { ["d"] = shape.Path };
            attributes["fill"] = WebRealizer.PaintCss(shape.Fill);
            // `currentColor` has no alpha of its own, so an inherited paint drawn at a fraction
            // says so beside it — a Solid one carries its alpha in the colour already.
            if (shape.Fill.Kind == VectorPaintKind.Inherit && shape.Fill.Alpha < 1)
                attributes["fill-opacity"] = TokenCss.Number(shape.Fill.Alpha);
            if (shape.Stroke.Paints)
            {
                attributes["stroke"] = WebRealizer.PaintCss(shape.Stroke);
                if (shape.Stroke.Kind == VectorPaintKind.Inherit && shape.Stroke.Alpha < 1)
                    attributes["stroke-opacity"] = TokenCss.Number(shape.Stroke.Alpha);
                attributes["stroke-width"] = TokenCss.Number(shape.StrokeWidth);
                attributes["stroke-linecap"] = "round";
                attributes["stroke-linejoin"] = "round";
            }
            if (shape.EvenOdd) attributes["fill-rule"] = "evenodd";
            if (shape.Opacity < 1) attributes["opacity"] = TokenCss.Number(shape.Opacity);
            path.RawAttributes = attributes;
            svg.Children.Add(path);
        }
        return svg;
    }


    private HtmlElement LowerGlyph(IconGlyph glyph, float width, float height, ColorToken? color, string? label)
    {
        var svg = new RealizedElement("svg")
        {
            Style = new HtmlStyle
            {
                // Block, not inline: an inline svg sits on the TEXT BASELINE of whatever box
                // holds it, and inherits a line box — a 9dp arrowhead inside a positioned
                // wrapper rendered ~a descender below the line it was aimed at. Flex items
                // ignore display, so every icon in a Row is unaffected.
                Display = Display.Block,
                Width = TokenCss.Px(width),
                Height = TokenCss.Px(height),
                Color = color is { } tint ? TokenCss.Value(tint) : null,
            },
        };
        svg.RawAttributes = new Dictionary<string, string>
        {
            ["viewBox"] = glyph.ViewBox,
        };
        // Fill glyphs are alpha masks; stroke glyphs are the outline family (2dp round — spec §07).
        if (glyph.Style == IconGlyphStyle.Stroke)
        {
            svg.RawAttributes["fill"] = "none";
            svg.RawAttributes["stroke"] = "currentColor";
            svg.RawAttributes["stroke-width"] = glyph.StrokeWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
            svg.RawAttributes["stroke-linecap"] = "round";
            svg.RawAttributes["stroke-linejoin"] = "round";
        }
        else
        {
            svg.RawAttributes["fill"] = "currentColor";
        }
        if (label is { })
        {
            // A10: "with label → image role". An aria-label on a bare <svg> gives the node a NAME
            // and no role, so a screen reader announces the words and cannot say what they belong
            // to — some read it as a group, some as nothing. The native bridges have emitted
            // SemanticRole.Image for a labelled glyph since the a11y pass (Semantics.cs), so this was
            // also the web disagreeing with its own twin.
            svg.RawAttributes["aria-label"] = label;
            svg.RawAttributes["role"] = "img";
        }
        else svg.RawAttributes["aria-hidden"] = "true";

        var glyphPath = new RealizedElement("path");
        glyphPath.RawAttributes = new Dictionary<string, string> { ["d"] = glyph.Path };
        svg.Children.Add(glyphPath);
        return svg;
    }
    /// <summary>
    /// The live surface (TS twin: lowerCameraPreview). No session — which is every SSR, since a
    /// stream only ever exists client-side — renders the SurfaceSubtle placeholder div both
    /// realizers agree on; with one, the muted autoplaying video the runtime wires by session id.
    /// </summary>
    private HtmlElement LowerCameraPreview(CameraPreview camera)
    {
        var style = new HtmlStyle
        {
            Width = TokenCss.Px(camera.Width),
            Height = TokenCss.Px(camera.Height),
            ObjectFit = "cover",
            Background = "var(--eq-color-surface-subtle)",
            BorderRadius = camera.CornerRadius.IsZero ? null : TokenCss.Radius(camera.CornerRadius),
        };
        if (camera.Session is null) return new RealizedElement("div") { Style = style };
        return new RealizedElement("video")
        {
            Style = style,
            RawAttributes = new Dictionary<string, string>
            {
                ["data-eq-camera"] = camera.Session.Id,
                ["autoplay"] = "",
                ["muted"] = "",
                ["playsinline"] = "",
                ["aria-label"] = camera.Label,
            },
        };
    }

    /// <summary>
    /// A sandboxed <c>iframe</c>. <c>sandbox</c> is ALWAYS present — an empty value is the locked
    /// frame, and each <see cref="WebSandbox"/> flag appends its <c>allow-</c> token. Inline
    /// <see cref="WebFrame.Document"/> wins over <see cref="WebFrame.Source"/>; the border is the
    /// frame's own 1990s default, so it goes.
    /// </summary>
    private HtmlElement LowerWebFrame(WebFrame frame)
    {
        var tokens = new List<string>(4);
        if (frame.Sandbox.HasFlag(WebSandbox.Scripts)) tokens.Add("allow-scripts");
        if (frame.Sandbox.HasFlag(WebSandbox.SameOrigin)) tokens.Add("allow-same-origin");
        if (frame.Sandbox.HasFlag(WebSandbox.Forms)) tokens.Add("allow-forms");
        if (frame.Sandbox.HasFlag(WebSandbox.Popups)) tokens.Add("allow-popups");

        var attributes = new Dictionary<string, string>
        {
            ["sandbox"] = string.Join(' ', tokens),
            ["title"] = frame.Title,
        };
        if (frame.Document is { Length: > 0 } document) attributes["srcdoc"] = document;
        else if (frame.Source is { Length: > 0 } source) attributes["src"] = source;

        return new RealizedElement("iframe")
        {
            Style = new HtmlStyle
            {
                Width = SizeCss(frame.Width),
                Height = SizeCss(frame.Height),
                Border = "0",
                Display = Display.Block,
                BorderRadius = frame.CornerRadius.IsZero ? null : TokenCss.Radius(frame.CornerRadius),
            },
            RawAttributes = attributes,
        };

        static string? SizeCss(SizeValue size) => size.Kind switch
        {
            SizeKind.Fixed => TokenCss.Px(size.Value),
            SizeKind.Fill => "100%",
            _ => null, // hug = the element's own default
        };
    }
    /// <summary>Spec A11 lowering: an explicitly sized <c>&lt;img&gt;</c> with object-fit and the
    /// rrect clip via border-radius; empty alt = decorative (HTML semantics).</summary>
    private HtmlElement LowerImage(Primitives.Image image)
    {
        var element = new RealizedElement("img")
        {
            Style = new HtmlStyle
            {
                Width = TokenCss.Px(image.Width),
                Height = TokenCss.Px(image.Height),
                ObjectFit = image.Fit switch
                {
                    ImageFit.Contain => "contain",
                    ImageFit.Stretch => "fill",
                    _ => "cover",
                },
                BorderRadius = image.CornerRadius.IsZero ? null : TokenCss.Radius(image.CornerRadius),
            },
            RawAttributes = new Dictionary<string, string>
            {
                ["src"] = image.Source,
                ["alt"] = image.Label,
            },
        };
        if (image.DarkSource is not { Length: > 0 } darkSource) return element;

        // A PAIR of artworks, resolved the way a ColorToken is: both ship, CSS shows one. The
        // generated .eq-themed-* rules follow the OS preference AND the app's own forced mode
        // (the theme controller stamps data-theme), so nothing decides this at paint time.
        element.ClassName = "eq-themed-light";
        var dark = new RealizedElement("img")
        {
            ClassName = "eq-themed-dark",
            Style = element.Style,
            RawAttributes = new Dictionary<string, string>
            {
                ["src"] = darkSource,
                ["alt"] = image.Label,
            },
        };
        var pair = new RealizedElement("span")
        {
            Style = new HtmlStyle { Display = Display.Contents },
        };
        pair.Children.Add(element);
        pair.Children.Add(dark);
        return pair;
    }
}
