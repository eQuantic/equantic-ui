namespace eQuantic.UI.Primitives;

/// <summary>Style channels a <see cref="TransitionSpec"/> animates — combine freely
/// (<c>Colors | Transform</c>). <see cref="All"/> is the whole style surface.</summary>
[Flags]
public enum StyleChannels : byte
{
    None = 0,
    /// <summary>Background, border and content colors (the hover-tint channel).</summary>
    Colors = 1,
    Opacity = 2,
    Transform = 4,
    /// <summary>box-shadow — elevation swaps and glow hovers.</summary>
    Shadow = 8,
    /// <summary>Element blur and backdrop blur (the scrolled header's frosted veil).</summary>
    Filters = 16,
    /// <summary>Width/height/max bounds — the panel-morph channel.</summary>
    Size = 32,
    All = Colors | Opacity | Transform | Shadow | Filters | Size,
}
