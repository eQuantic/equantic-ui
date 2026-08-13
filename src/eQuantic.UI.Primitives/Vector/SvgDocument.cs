namespace eQuantic.UI.Primitives;

/// <summary>
/// Reads an SVG document into a <see cref="VectorDrawing"/> — the whole point being that it happens
/// ONCE, away from any screen, and what both realizers get afterwards is data.
/// <para>
/// The subset is deliberate and named, the way the Markdown one is. Understood: <c>viewBox</c>,
/// <c>path</c>, <c>rect</c> (including <c>rx</c>/<c>ry</c>), <c>circle</c>, <c>ellipse</c>,
/// <c>line</c>, <c>polyline</c>, <c>polygon</c>, <c>g</c> with nesting, <c>transform</c>
/// (translate/scale/rotate/skewX/skewY/matrix), <c>fill</c>, <c>stroke</c>, <c>stroke-width</c>,
/// <c>fill-rule</c>, the three opacities, and the same properties written in a <c>style</c>
/// attribute. Colors as <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c>, <c>rgba()</c>,
/// <c>none</c>, <c>currentColor</c>, and the basic named set.
/// </para>
/// <para>
/// DROPPED, deliberately and silently: <c>defs</c>, gradients, patterns, <c>clipPath</c>,
/// <c>mask</c>, <c>filter</c>, <c>use</c>, <c>image</c>, and <c>text</c> — every one of them either
/// needs a paint server this model has no room for, or needs a font, and half-rendering artwork is
/// worse than a fence a reader can see. A file that leans on them arrives with those shapes
/// missing rather than wrong.
/// </para>
/// <para>
/// A group's <c>transform</c> is BAKED into the path data it wraps rather than carried, because the
/// two targets do not agree about transforms — see <see cref="VectorTransform"/>.
/// </para>
/// </summary>
public static class SvgDocument
{
    /// <summary>Elements whose entire SUBTREE is skipped: they describe paint or geometry this
    /// model cannot carry, and their children are not drawings in their own right.</summary>
    private static readonly HashSet<string> Skipped =
        ["defs", "clipPath", "mask", "filter", "symbol", "marker", "pattern", "text", "style",
         "linearGradient", "radialGradient", "metadata", "title", "desc"];

    public static VectorDrawing Parse(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg)) return VectorDrawing.Empty;

        var shapes = new List<VectorShape>();
        var inherited = new Stack<PaintState>();
        var state = PaintState.Root;
        var (minX, minY, width, height) = (0f, 0f, 0f, 0f);
        var seenSvg = false;
        var skipDepth = 0;

        var i = 0;
        while (i < svg.Length)
        {
            if (svg[i] != '<') { i++; continue; }

            // Declarations, comments and doctypes carry nothing this reader wants.
            if (svg.AsSpan(i).StartsWith("<!--"))
            {
                var end = svg.IndexOf("-->", i, StringComparison.Ordinal);
                i = end < 0 ? svg.Length : end + 3;
                continue;
            }
            if (svg.AsSpan(i).StartsWith("<?") || svg.AsSpan(i).StartsWith("<!"))
            {
                var end = svg.IndexOf('>', i);
                i = end < 0 ? svg.Length : end + 1;
                continue;
            }

            var closing = i + 1 < svg.Length && svg[i + 1] == '/';
            var tagEnd = svg.IndexOf('>', i);
            if (tagEnd < 0) break;
            var selfClosing = tagEnd > i && svg[tagEnd - 1] == '/';
            var inner = svg.Substring(i + (closing ? 2 : 1), tagEnd - i - (closing ? 2 : 1) - (selfClosing ? 1 : 0));
            i = tagEnd + 1;

            var name = ElementName(inner);
            if (name.Length == 0) continue;

            if (closing)
            {
                if (skipDepth > 0) { skipDepth--; continue; }
                if (name == "g" && inherited.Count > 0) state = inherited.Pop();
                continue;
            }

            if (skipDepth > 0)
            {
                if (!selfClosing) skipDepth++;
                continue;
            }

            if (Skipped.Contains(name))
            {
                if (!selfClosing) skipDepth = 1;
                continue;
            }

            var attributes = Attributes(inner);

            if (name == "svg")
            {
                if (seenSvg) continue;   // a nested <svg> re-frames its children: out of subset
                seenSvg = true;
                (minX, minY, width, height) = ViewBox(attributes);
                state = state.Inherit(attributes);
                continue;
            }

            if (name == "g")
            {
                inherited.Push(state);
                state = state.Inherit(attributes);
                // A group that is not closed (self-closing <g/>) has nothing inside it to inherit.
                if (selfClosing && inherited.Count > 0) state = inherited.Pop();
                continue;
            }

            var data = PathDataOf(name, attributes);
            if (data.Length == 0) continue;

            var shapeState = state.Inherit(attributes);
            var path = VectorPath.Transform(data, shapeState.Transform);
            // A shape with neither paint draws nothing — usually a hit-test rect an exporter left
            // behind. Dropping it keeps the raster count honest on the native side.
            if (!shapeState.Fill.Paints && !shapeState.Stroke.Paints) continue;

            shapes.Add(new VectorShape(path, shapeState.Fill, shapeState.Stroke,
                shapeState.StrokeWidth * Scale(shapeState.Transform), shapeState.EvenOdd,
                shapeState.Opacity));
        }

        if (!seenSvg) return VectorDrawing.Empty;
        if (width <= 0 || height <= 0) return VectorDrawing.Empty;
        return new VectorDrawing(minX, minY, width, height, shapes);
    }

    /// <summary>
    /// What a transform does to a stroke's WIDTH. A stroke is drawn in user units, so baking a
    /// group's scale into the path data without scaling the pen would leave a scaled shape wearing
    /// an unscaled outline. Non-uniform scale has no single answer — the geometric mean is the one
    /// that keeps the total ink right.
    /// </summary>
    private static float Scale(VectorTransform transform)
    {
        var determinant = MathF.Abs(transform.A * transform.D - transform.B * transform.C);
        return determinant <= 0 ? 1 : MathF.Sqrt(determinant);
    }

    /// <summary>The drawing's own grid: the viewBox if there is one, else the width/height the
    /// document was authored at — a file with neither cannot be placed and becomes empty.</summary>
    private static (float, float, float, float) ViewBox(Dictionary<string, string> attributes)
    {
        if (attributes.TryGetValue("viewBox", out var box))
        {
            var numbers = Numbers(box);
            if (numbers.Count >= 4) return (numbers[0], numbers[1], numbers[2], numbers[3]);
        }
        var width = Length(attributes, "width");
        var height = Length(attributes, "height");
        return (0, 0, width, height);
    }

    /// <summary>Every SVG shape as path data. A rect is a path, a circle is a path — one geometry
    /// vocabulary downstream instead of six.</summary>
    private static string PathDataOf(string name, Dictionary<string, string> attributes)
    {
        switch (name)
        {
            case "path":
                return attributes.TryGetValue("d", out var d) ? d.Trim() : "";

            case "rect":
            {
                var x = Length(attributes, "x");
                var y = Length(attributes, "y");
                var width = Length(attributes, "width");
                var height = Length(attributes, "height");
                if (width <= 0 || height <= 0) return "";
                var rx = Length(attributes, "rx");
                var ry = Length(attributes, "ry");
                // One radius given means both, which is what the spec says and what exporters emit.
                if (rx <= 0 && ry > 0) rx = ry;
                if (ry <= 0 && rx > 0) ry = rx;
                rx = MathF.Min(rx, width / 2);
                ry = MathF.Min(ry, height / 2);
                if (rx <= 0 || ry <= 0)
                    return $"M{N(x)} {N(y)}H{N(x + width)}V{N(y + height)}H{N(x)}Z";
                return $"M{N(x + rx)} {N(y)}"
                     + $"H{N(x + width - rx)}A{N(rx)} {N(ry)} 0 0 1 {N(x + width)} {N(y + ry)}"
                     + $"V{N(y + height - ry)}A{N(rx)} {N(ry)} 0 0 1 {N(x + width - rx)} {N(y + height)}"
                     + $"H{N(x + rx)}A{N(rx)} {N(ry)} 0 0 1 {N(x)} {N(y + height - ry)}"
                     + $"V{N(y + ry)}A{N(rx)} {N(ry)} 0 0 1 {N(x + rx)} {N(y)}Z";
            }

            case "circle":
            case "ellipse":
            {
                var cx = Length(attributes, "cx");
                var cy = Length(attributes, "cy");
                var rx = name == "circle" ? Length(attributes, "r") : Length(attributes, "rx");
                var ry = name == "circle" ? rx : Length(attributes, "ry");
                if (rx <= 0 || ry <= 0) return "";
                // Two half-arcs: one arc cannot express a full ellipse (its endpoints coincide).
                return $"M{N(cx - rx)} {N(cy)}"
                     + $"A{N(rx)} {N(ry)} 0 1 0 {N(cx + rx)} {N(cy)}"
                     + $"A{N(rx)} {N(ry)} 0 1 0 {N(cx - rx)} {N(cy)}Z";
            }

            case "line":
                return $"M{N(Length(attributes, "x1"))} {N(Length(attributes, "y1"))}"
                     + $"L{N(Length(attributes, "x2"))} {N(Length(attributes, "y2"))}";

            case "polyline":
            case "polygon":
            {
                if (!attributes.TryGetValue("points", out var points)) return "";
                var numbers = Numbers(points);
                if (numbers.Count < 4) return "";
                var text = new System.Text.StringBuilder();
                for (var p = 0; p + 1 < numbers.Count; p += 2)
                    text.Append(p == 0 ? 'M' : 'L').Append(N(numbers[p])).Append(' ').Append(N(numbers[p + 1]));
                if (name == "polygon") text.Append('Z');
                return text.ToString();
            }

            default:
                return "";
        }
    }

    /// <summary>The painting state an element inherits and may override — SVG's own model, which is
    /// why a <c>&lt;g fill="…"&gt;</c> around ten paths is the usual way artwork is exported.</summary>
    private readonly record struct PaintState(
        VectorTransform Transform, VectorPaint Fill, VectorPaint Stroke,
        float StrokeWidth, bool EvenOdd, float Opacity)
    {
        /// <summary>SVG's initial values: filled black, unstroked, a pen one unit wide.</summary>
        public static readonly PaintState Root = new(
            VectorTransform.Identity, VectorPaint.Solid(Color.Black), VectorPaint.None, 1, false, 1);

        public PaintState Inherit(Dictionary<string, string> attributes)
        {
            var next = this;
            if (attributes.TryGetValue("transform", out var transform))
                next = next with { Transform = next.Transform.Compose(ParseTransform(transform)) };

            // `style` wins over the presentation attributes, which is the cascade's answer and what
            // every exporter relies on when it writes both.
            var properties = Style(attributes);

            if (Property(attributes, properties, "fill") is { } fill)
                next = next with { Fill = ParsePaint(fill) };
            if (Property(attributes, properties, "stroke") is { } stroke)
                next = next with { Stroke = ParsePaint(stroke) };
            if (Property(attributes, properties, "stroke-width") is { } strokeWidth
                && TryFloat(strokeWidth, out var pen))
                next = next with { StrokeWidth = pen };
            if (Property(attributes, properties, "fill-rule") is { } rule)
                next = next with { EvenOdd = rule.Trim() == "evenodd" };
            if (Property(attributes, properties, "opacity") is { } opacity && TryFloat(opacity, out var alpha))
                next = next with { Opacity = Math.Clamp(next.Opacity * alpha, 0, 1) };

            // The per-paint opacities fold into the COLOR's alpha, where they belong: they are not
            // the element's opacity and do not compose with it the same way.
            if (Property(attributes, properties, "fill-opacity") is { } fillOpacity
                && TryFloat(fillOpacity, out var fillAlpha))
                next = next with { Fill = WithOpacity(next.Fill, fillAlpha) };
            if (Property(attributes, properties, "stroke-opacity") is { } strokeOpacity
                && TryFloat(strokeOpacity, out var strokeAlpha))
                next = next with { Stroke = WithOpacity(next.Stroke, strokeAlpha) };

            return next;
        }

        /// <summary>`fill-opacity` on ANY paint that draws — an inherited one keeps it in the alpha
        /// slot its colour does not use, so `currentColor` at 55% survives to the realizer.</summary>
        private static VectorPaint WithOpacity(VectorPaint paint, float opacity) =>
            paint.Kind == VectorPaintKind.None
                ? paint
                : paint with { Color = paint.Color.WithOpacity(Math.Clamp(opacity, 0, 1)) };
    }

    private static string? Property(Dictionary<string, string> attributes,
        Dictionary<string, string>? style, string name)
    {
        if (style is not null && style.TryGetValue(name, out var fromStyle)) return fromStyle;
        return attributes.TryGetValue(name, out var value) ? value : null;
    }

    private static Dictionary<string, string>? Style(Dictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue("style", out var style)) return null;
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var declaration in style.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon <= 0) continue;
            properties[declaration[..colon].Trim()] = declaration[(colon + 1)..].Trim();
        }
        return properties;
    }

    /// <summary>The transform LIST, applied left to right — `translate(4 2) scale(2)` scales first
    /// and then translates, which is the order the attribute reads in.</summary>
    private static VectorTransform ParseTransform(string text)
    {
        var result = VectorTransform.Identity;
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ',')) i++;
            var start = i;
            while (i < text.Length && (char.IsLetter(text[i]))) i++;
            if (i == start) break;
            var name = text[start..i];
            var open = text.IndexOf('(', i);
            var close = open < 0 ? -1 : text.IndexOf(')', open);
            if (open < 0 || close < 0) break;
            var numbers = Numbers(text[(open + 1)..close]);
            i = close + 1;

            var step = name switch
            {
                "translate" when numbers.Count >= 1 =>
                    VectorTransform.Translate(numbers[0], numbers.Count > 1 ? numbers[1] : 0),
                "scale" when numbers.Count >= 1 =>
                    VectorTransform.Scale(numbers[0], numbers.Count > 1 ? numbers[1] : numbers[0]),
                "rotate" when numbers.Count >= 3 =>
                    // Rotation about a point: move it to the origin, turn, move it back.
                    VectorTransform.Translate(numbers[1], numbers[2])
                        .Compose(VectorTransform.Rotate(numbers[0]))
                        .Compose(VectorTransform.Translate(-numbers[1], -numbers[2])),
                "rotate" when numbers.Count >= 1 => VectorTransform.Rotate(numbers[0]),
                "skewX" when numbers.Count >= 1 => VectorTransform.SkewX(numbers[0]),
                "skewY" when numbers.Count >= 1 => VectorTransform.SkewY(numbers[0]),
                "matrix" when numbers.Count >= 6 =>
                    new VectorTransform(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]),
                _ => VectorTransform.Identity,
            };
            result = result.Compose(step);
        }
        return result;
    }

    private static VectorPaint ParsePaint(string text)
    {
        var value = text.Trim();
        if (value.Length == 0 || value == "none" || value == "transparent") return VectorPaint.None;
        if (value == "currentColor" || value == "inherit") return VectorPaint.Inherit;
        // A paint server (`url(#gradient)`) is out of subset: the shape is dropped rather than
        // filled with a colour nobody chose.
        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase)) return VectorPaint.None;
        return VectorPaint.Solid(ParseColor(value));
    }

    private static Color ParseColor(string value)
    {
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            static byte Nibble(char c) => (byte)(Convert.ToInt32(c.ToString(), 16) * 17);
            static byte Byte(string h, int at) => (byte)Convert.ToInt32(h.Substring(at, 2), 16);
            try
            {
                return hex.Length switch
                {
                    3 => Color.FromRgb(Nibble(hex[0]), Nibble(hex[1]), Nibble(hex[2])),
                    4 => Color.FromRgba(Nibble(hex[0]), Nibble(hex[1]), Nibble(hex[2]), Nibble(hex[3])),
                    6 => Color.FromRgb(Byte(hex, 0), Byte(hex, 2), Byte(hex, 4)),
                    8 => Color.FromRgba(Byte(hex, 0), Byte(hex, 2), Byte(hex, 4), Byte(hex, 6)),
                    _ => Color.Black,
                };
            }
            catch (FormatException)
            {
                return Color.Black;   // a malformed colour is black, never a throw at build time
            }
        }

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = value.IndexOf('(');
            var close = value.LastIndexOf(')');
            if (open > 0 && close > open)
            {
                var numbers = Numbers(value[(open + 1)..close]);
                if (numbers.Count >= 3)
                {
                    // The alpha channel is 0..1 in rgba(), unlike the colour channels. ROUNDED, not
                    // truncated: `fill-opacity` goes through Color.WithOpacity, which rounds, and
                    // two spellings of half-transparent must not land a step apart.
                    var alpha = numbers.Count >= 4 ? Channel(numbers[3] * 255) : (byte)255;
                    return Color.FromRgba(Channel(numbers[0]), Channel(numbers[1]), Channel(numbers[2]), alpha);
                }
            }
            return Color.Black;
        }

        return Named(value);
    }

    private static byte Channel(float value) => (byte)Math.Clamp(MathF.Round(value), 0, 255);

    /// <summary>
    /// The basic colour keywords. Not the full CSS list on purpose: these are the ones artwork
    /// actually uses, and an unknown name resolving to BLACK is the same answer a browser gives a
    /// misspelling.
    /// </summary>
    private static Color Named(string value) => value.ToLowerInvariant() switch
    {
        "black" => Color.Black,
        "white" => Color.White,
        "red" => Color.FromRgb(255, 0, 0),
        "lime" => Color.FromRgb(0, 255, 0),
        "green" => Color.FromRgb(0, 128, 0),
        "blue" => Color.FromRgb(0, 0, 255),
        "navy" => Color.FromRgb(0, 0, 128),
        "yellow" => Color.FromRgb(255, 255, 0),
        "orange" => Color.FromRgb(255, 165, 0),
        "purple" => Color.FromRgb(128, 0, 128),
        "fuchsia" or "magenta" => Color.FromRgb(255, 0, 255),
        "aqua" or "cyan" => Color.FromRgb(0, 255, 255),
        "teal" => Color.FromRgb(0, 128, 128),
        "maroon" => Color.FromRgb(128, 0, 0),
        "olive" => Color.FromRgb(128, 128, 0),
        "silver" => Color.FromRgb(192, 192, 192),
        "gray" or "grey" => Color.FromRgb(128, 128, 128),
        _ => Color.Black,
    };

    private static string ElementName(string inner)
    {
        var end = 0;
        while (end < inner.Length && !char.IsWhiteSpace(inner[end]) && inner[end] != '/') end++;
        var name = inner[..end];
        // Namespace prefixes (`svg:path`) name the same element.
        var colon = name.IndexOf(':');
        return colon >= 0 ? name[(colon + 1)..] : name;
    }

    private static Dictionary<string, string> Attributes(string inner)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var i = 0;
        while (i < inner.Length && !char.IsWhiteSpace(inner[i])) i++;   // past the element name

        while (i < inner.Length)
        {
            while (i < inner.Length && (char.IsWhiteSpace(inner[i]) || inner[i] == '/')) i++;
            var start = i;
            while (i < inner.Length && inner[i] != '=' && !char.IsWhiteSpace(inner[i])) i++;
            if (i >= inner.Length || start == i) break;
            var name = inner[start..i];
            while (i < inner.Length && char.IsWhiteSpace(inner[i])) i++;
            if (i >= inner.Length || inner[i] != '=') continue;
            i++;
            while (i < inner.Length && char.IsWhiteSpace(inner[i])) i++;
            if (i >= inner.Length) break;
            var quote = inner[i];
            if (quote is not ('"' or '\'')) continue;
            i++;
            var valueStart = i;
            while (i < inner.Length && inner[i] != quote) i++;
            var value = inner[valueStart..Math.Min(i, inner.Length)];
            i = Math.Min(i + 1, inner.Length);

            var colon = name.IndexOf(':');
            if (colon >= 0) name = name[(colon + 1)..];   // xlink:href, svg:width…
            attributes[name] = Decode(value);
        }
        return attributes;
    }

    /// <summary>The five XML entities. Attribute values carry them, and path data with a raw
    /// ampersand in it is not something an exporter writes.</summary>
    private static string Decode(string value) =>
        value.IndexOf('&') < 0
            ? value
            : value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"")
                   .Replace("&apos;", "'").Replace("&amp;", "&");

    /// <summary>A length attribute, tolerating the unit suffixes a document may carry
    /// (<c>24px</c>): the drawing's grid is its viewBox, so the number is what matters.</summary>
    private static float Length(Dictionary<string, string> attributes, string name)
    {
        if (!attributes.TryGetValue(name, out var text)) return 0;
        var i = 0;
        return VectorPath.TryNumber(text, ref i, out var value) ? value : 0;
    }

    private static bool TryFloat(string text, out float value)
    {
        var i = 0;
        return VectorPath.TryNumber(text, ref i, out value);
    }

    private static List<float> Numbers(string text)
    {
        var numbers = new List<float>();
        var i = 0;
        while (i < text.Length)
        {
            var before = i;
            if (VectorPath.TryNumber(text, ref i, out var value)) numbers.Add(value);
            if (i == before) i++;   // junk between numbers, keep walking
        }
        return numbers;
    }

    private static string N(float value) =>
        MathF.Round(value, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
