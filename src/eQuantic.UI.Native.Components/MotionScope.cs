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
    public float RenderScale { get; init; } = 1f;
    public float TypeScale { get; init; } = 1f;
    public bool Active { get; set; }

    /// <summary>The host's presence clock — the emit pass snapshots each live presence subtree's
    /// commands into it (the exit replay source). Null = no exit machinery (layout-only tests).</summary>
    public PresenceStore? Presences { get; init; }
    /// <summary>Spec S6 at PAINT time: the same store layout uses for flex weights, so a box's
    /// colours, opacity, transform and shadow glide under its own <c>Transition</c>.</summary>
    public TransitionStore? Transitions { get; init; }
}
