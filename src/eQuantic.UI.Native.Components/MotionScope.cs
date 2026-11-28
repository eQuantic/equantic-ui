using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>The frame clock for loop motion: offsets resolve as a PURE function of
/// <see cref="TimeMs"/> (deterministic frames — goldens pin a fixed t). Reduce Motion (spec §06)
/// replaces movement statically: the child renders at rest and the frame reports no active
/// motion, so the host stops burning frames.</summary>
/// <para>Top-level rather than nested since S6: the emit pass moved out of <see cref="PhotonRealizer"/>
/// into <c>EmitVisitor</c>, and this is that pass's clock. It is the realizer that hands one over,
/// not the realizer that reads it.</para>
internal sealed class MotionScope
{
    public MotionScope(float timeMs, bool reduced)
    {
        TimeMs = timeMs;
        Reduced = reduced;
    }

    public float TimeMs { get; }
    public bool Reduced { get; }

    /// <summary>Wave 3: anchored panels position against the viewport (Top/End placements).</summary>
    public float ViewportW { get; init; }
    public float ViewportH { get; init; }

    /// <summary>W4: the platform text service + per-host raster cache (null = placeholder bars).</summary>
    public Framework.ITextRasterizer? TextRasterizer { get; init; }
    public TextRasterCache? TextCache { get; init; }
    public Framework.IImageLoader? ImageLoader { get; init; }
    public Dictionary<string, TextureData?>? ImageCache { get; init; }

    /// <summary>W4: the platform icon service + per-host raster cache (null = disc placeholder).</summary>
    public Framework.IIconRasterizer? IconRasterizer { get; init; }
    public IconRasterCache? IconCache { get; init; }

    /// <summary>The host's cache of the tiles a box's patterns composite (#1937); null = the shared one.</summary>
    public PatternTileCache? PatternCache { get; init; }

    /// <summary>
    /// The transform the frame's root is drawn under, the render scale and what the host lays over it:
    /// what maps the window, the root's own box, into the device. A window-attached pattern is placed in the
    /// window mapped from here into the box's own space (#1937), so a scroll, which moves the box and not
    /// the window, leaves its tiles where they were.
    /// </summary>
    public Matrix2D Root { get; init; } = Matrix2D.Identity;

    public float RenderScale { get; init; } = 1f;
    public float TypeScale { get; init; } = 1f;
    public bool Active { get; set; }

    /// <summary>The host's presence clock — the emit pass snapshots each live presence subtree's
    /// commands onto its position (the exit replay source). Null = no exit machinery (layout-only
    /// tests).</summary>
    public PresenceClock? Presences { get; init; }
    /// <summary>Spec S6 at PAINT time: the same clock layout uses for flex weights, so a box's
    /// colours, opacity, transform and shadow glide under its own <c>Transition</c>.</summary>
    public TransitionClock? Transitions { get; init; }

    /// <summary>
    /// This clock without the clocks that REMEMBER (presences and transitions), for a subtree drawn a second
    /// time as a picture: a sortable item's copy (#1537). Its paint would otherwise become the subtree's
    /// exit snapshot, and its resting colours a transition the subtree glides toward and back every frame.
    /// </summary>
    /// <summary>
    /// <paramref name="source"/> decoded by the host's image loader, once per source (<see cref="ImageCache"/>):
    /// what an <see cref="Image"/> draws and an <see cref="ImagePattern"/> tiles, so a source both use is
    /// decoded once. Null with no loader, or where the loader could not decode it.
    /// </summary>
    public TextureData? Decoded(string source)
    {
        if (ImageLoader is not { } loader) return null;
        if (ImageCache is { } cache && cache.TryGetValue(source, out var known)) return known;
        var decoded = loader.Load(source);
        var data = decoded is null ? null : TextureData.Rgba(decoded.Width, decoded.Height, decoded.Rgba);
        ImageCache?[source] = data;
        return data;
    }

    public MotionScope Still() => new(TimeMs, Reduced)
    {
        ViewportW = ViewportW,
        ViewportH = ViewportH,
        TextRasterizer = TextRasterizer,
        TextCache = TextCache,
        ImageLoader = ImageLoader,
        ImageCache = ImageCache,
        IconRasterizer = IconRasterizer,
        IconCache = IconCache,
        PatternCache = PatternCache,
        Root = Root,
        RenderScale = RenderScale,
        TypeScale = TypeScale,
    };
}
