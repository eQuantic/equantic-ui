using Android.Graphics;
using Android.Text;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
// Both the platform and the vocabulary have a Color. Here the platform's is the one being set on a
// platform Paint; the engine's never reaches this file, because a coverage raster carries no colour.
using Canvas = Android.Graphics.Canvas;
using Color = Android.Graphics.Color;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Text on Android, through the platform's own shaper — a system framework, like Metal and CoreText
/// are on Apple, and no third party. ONE service measures and rasterizes, because a layout broken
/// by one set of metrics and drawn with another is a layout that disagrees with itself.
/// <para>
/// <see cref="StaticLayout"/> decides where the lines BREAK, which is the part a shaper must own.
/// Where they SIT is the design system's decision: each line is drawn on the style's line-height
/// grid rather than on the font's, which is the same contract CoreText is held to.
/// </para>
/// </summary>
public sealed class AndroidTextService : ITextMeasurer, ITextRasterizer
{
    private readonly Dictionary<(float Size, FontWeight Weight, bool Mono, bool Italic, FontFamily? Family), TextPaint> _paints = new();

    // A face the app SHIPS (#1055). Android opens a typeface from a FILE, so each one is written once
    // to the app's cache, whole or not at all (ShippedFaces.CacheFile), and opened there: one typeface
    // per file, the one a style asks for chosen by its slant and its weight (ShippedFace).
    private static readonly ShippedFaces Shipped = new(Register);
    private static readonly Dictionary<FontFile, Typeface> ShippedTypefaces = new();

    private static bool Register(FontFile file, byte[] bytes)
    {
        if (global::Android.App.Application.Context.CacheDir?.AbsolutePath is not { } cache
            || ShippedFaces.CacheFile(System.IO.Path.Combine(cache, "eq-fonts"), file, bytes) is not { } path) return false;
        Typeface? typeface;
        try
        {
            // A file the platform cannot load THROWS rather than answering null.
            typeface = Typeface.CreateFromFile(path);
        }
        catch (Java.Lang.RuntimeException)
        {
            return false;
        }
        if (typeface is null) return false;
        lock (ShippedTypefaces) ShippedTypefaces[file] = typeface;
        return true;
    }

    /// <summary>
    /// The typeface of a shipped <paramref name="family"/> for a style, and its file: the one that sets the
    /// weight and the slant, chosen as every shell chooses it (ShippedFaces.FileFor, a browser's rule). Null
    /// when its files did not all register: the family resolves from them whole or not at all, so a missing
    /// cut is reported and falls back rather than being drawn from another.
    /// </summary>
    private static (Typeface Face, FontFile File)? ShippedFace(FontFamily family, FontWeight weight, bool italic)
    {
        if (!Shipped.Register(family) || ShippedFaces.FileFor(family, weight, italic) is not { } file) return null;
        lock (ShippedTypefaces) return ShippedTypefaces.TryGetValue(file, out var face) ? (face, file) : null;
    }

    public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
    {
        var lineHeight = style.ScaledLineHeight(typeScale);
        var lines = Break(content, style, typeScale, maxWidth, maxLines);
        var width = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
        return new TextMeasurement(width, lines.Count * lineHeight, lineHeight,
            lines.Select(line => new MeasuredLine(line.Width, line.Ellipsized)).ToArray());
    }

    public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines,
        float scale, TextAlignment align)
    {
        if (content.Length == 0) return null;

        var lineHeight = style.ScaledLineHeight(typeScale);
        var lines = Break(content, style, typeScale, maxWidth, maxLines);
        if (lines.Count == 0) return null;

        var paint = PaintFor(style.ScaledSize(typeScale) * scale, style.Weight, style.Mono, style.Italic, style.Family);
        // Measured with the SCALED paint, because a glyph's advance is not exactly linear in its
        // size: a width taken at 1x and multiplied comes out a hair short, and the last character
        // of every line is what gets sliced off.
        var lineWidths = lines.Select(line => paint.MeasureText(line.Text)).ToArray();
        // DEVICE pixels here, not dp: the widths come from the scaled paint, so the box has to be
        // scaled to meet them before anything is centred against it.
        var blockPx = align.BlockWidth(lineWidths.Max(),
            float.IsFinite(maxWidth) ? maxWidth * scale : maxWidth);
        var width = (int)MathF.Ceiling(blockPx);
        var height = (int)MathF.Ceiling(lines.Count * lineHeight * scale);
        if (width <= 0 || height <= 0) return null;
        var metrics = paint.GetFontMetrics()!;
        // The baseline within each line box: the STYLE's grid, with the font centred inside it.
        var baseline = (lineHeight * scale - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;

        using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        paint.Color = Color.White;
        // Placed line by line, so alignment is an x per line — the same arithmetic the Mac and
        // the headless bars use, rather than StaticLayout's own alignment, which this draw path
        // never goes through.
        for (var i = 0; i < lines.Count; i++)
            canvas.DrawText(lines[i].Text, align.Offset(blockPx, lineWidths[i]),
                i * lineHeight * scale + baseline, paint);

        // A8 COVERAGE is what the engine's textured pipeline samples: the tint belongs to the draw
        // command, so one raster serves light and dark alike.
        var argb = new int[width * height];
        bitmap.GetPixels(argb, 0, width, 0, 0, width, height);
        var alpha = new byte[argb.Length];
        for (var i = 0; i < argb.Length; i++) alpha[i] = (byte)((argb[i] >> 24) & 0xFF);

        return new TextRaster(width, height, alpha);
    }

    /// <summary>
    /// The generic families AOSP's <c>fonts.xml</c> declares as ALIASES — the ones the platform
    /// always answers, by name. Android hands them back as the default typeface, which is the right
    /// answer and indistinguishable from the wrong one, so they are named rather than compared.
    ///
    /// <para>
    /// Named EXACTLY, never by prefix. A prefix test on "sans-serif" exempts
    /// <c>sans-serif-not-installed</c> too, and an exempted family is one whose absence is never
    /// reported — the instrument turned off for precisely the values it exists to catch. (Second
    /// time a <c>StartsWith</c> has done this here: the dev-source announce line silenced
    /// <c>equantic-ui-web</c> the same way.)
    /// </para>
    /// </summary>
    private static readonly string[] SystemAliases =
    [
        "sans-serif", "sans-serif-condensed", "sans-serif-condensed-light", "sans-serif-thin",
        "sans-serif-light", "sans-serif-medium", "sans-serif-black", "sans-serif-smallcaps",
        "sans-serif-monospace", "serif", "serif-monospace", "monospace", "casual", "cursive",
    ];

    private static bool IsSystemAlias(string family) =>
        Array.Exists(SystemAliases, alias => alias.Equals(family, StringComparison.OrdinalIgnoreCase));

    private readonly record struct Line(string Text, float Width, bool Ellipsized);

    /// <summary>Where the lines break, and what each one ends up saying.</summary>
    private List<Line> Break(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
    {
        var paint = PaintFor(style.ScaledSize(typeScale), style.Weight, style.Mono, style.Italic, style.Family);
        // An unconstrained paragraph still needs a number; the text's own width is the smallest one
        // that changes nothing.
        var wrapAt = float.IsPositiveInfinity(maxWidth)
            ? (int)MathF.Ceiling(paint.MeasureText(content)) + 1
            : Math.Max(1, (int)MathF.Floor(maxWidth));

        var builder = StaticLayout.Builder.Obtain(content, 0, content.Length, paint, wrapAt)!
            .SetIncludePad(false)!;
        if (maxLines > 0) builder = builder.SetMaxLines(maxLines)!.SetEllipsize(TextUtils.TruncateAt.End)!;

        using var layout = builder.Build()!;
        var lines = new List<Line>(layout.LineCount);
        for (var i = 0; i < layout.LineCount; i++)
        {
            var text = content[layout.GetLineStart(i)..layout.GetLineEnd(i)].TrimEnd('\n');
            var ellipsized = layout.GetEllipsisCount(i) > 0;
            if (ellipsized)
            {
                // The layout says how much it dropped; the glyph it would have drawn is ours.
                var start = Math.Clamp(layout.GetEllipsisStart(i), 0, text.Length);
                text = text[..start].TrimEnd() + "…";
            }
            lines.Add(new Line(text, paint.MeasureText(text), ellipsized));
        }

        return lines;
    }

    private TextPaint PaintFor(float size, FontWeight weight, bool mono, bool italic, FontFamily? family = null)
    {
        if (_paints.TryGetValue((size, weight, mono, italic, family), out var cached)) return cached;

        var paint = new TextPaint(PaintFlags.AntiAlias | PaintFlags.SubpixelText)
        {
            TextSize = size,
            TextAlign = Paint.Align.Left,
        };
        // The FAMILY carries the face (the platform's own monospace), the STYLE carries the two
        // axes — and they are asked for together, because Android has one enum for the pair:
        // BoldItalic is not Bold applied twice, it is a fourth value. The style is what a version
        // below Android 9 can be asked for, and what tells a family the machine has from one it does
        // not; the weight itself is asked for where it can be, below.
        var bold = weight >= FontWeight.SemiBold;
        var style = (bold, italic) switch
        {
            (true, true) => TypefaceStyle.BoldItalic,
            (true, false) => TypefaceStyle.Bold,
            (false, true) => TypefaceStyle.Italic,
            _ => TypefaceStyle.Normal,
        };
        // Android substitutes for an unknown family like every other engine: Typeface.Create hands
        // back the DEFAULT rather than null, so the answer is compared against that default instead
        // of trusted.
        //
        // The system ALIASES are exempt from that comparison, and they have to be: "sans-serif"
        // resolves to the default typeface, so comparing would report the platform's own family as
        // missing — and an instrument that cries wolf on a correct value is one nobody reads by the
        // end of the week. What is left is a brand face, which is the case this exists for.
        var baseFace = mono ? Typeface.Monospace : Typeface.Default;
        var asked = (int)weight;   // the weight the face is asked for, from Android 9
        if (family is { Files.Count: > 0 })
        {
            if (ShippedFace(family, weight, italic) is { } shipped)
            {
                baseFace = shipped.Face;
                if (OperatingSystem.IsAndroidVersionAtLeast(28) && ShippedFaces.IsDrawnAsItIs(shipped.File, weight))
                    asked = shipped.Face.Weight;
            }
            else eQuantic.UI.Primitives.FaceResolution.Missing(family.Name);
        }
        else if (family is { Name.Length: > 0 })
        {
            var named = Typeface.Create(family.Name, style);
            if (named is null)
                eQuantic.UI.Primitives.FaceResolution.Missing(family.Name);
            else if (IsSystemAlias(family.Name) || !named.Equals(Typeface.Create(Typeface.Default, style)))
                baseFace = named;
            else
                eQuantic.UI.Primitives.FaceResolution.Missing(family.Name);
        }
        // From Android 9 (API 28) a typeface is asked for the weight itself, so every one of the nine
        // draws as named and a family that ships a file per weight draws from the file of the weight
        // (#1082). Below it the shell has regular and bold, which is all those versions take.
        //
        // A shipped file drawn as it is is asked for at the weight the typeface reads in its file: the
        // engine lays a fake bold over a face asked for 200 above that weight, and a file the catalog
        // weighs at 700 that declares 400 drew bolder than on every other target (#1109). A heavier
        // weight than a lighter file sets is asked for as it is, and the fake bold is what it lacks.
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            paint.SetTypeface(Typeface.Create(baseFace, asked, italic));
        else
            paint.SetTypeface(Typeface.Create(baseFace, style));
        _paints[(size, weight, mono, italic, family)] = paint;
        return paint;
    }
}
