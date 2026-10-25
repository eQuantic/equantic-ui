namespace eQuantic.UI.Primitives;

/// <summary>
/// Corner-shape scale — a semantic ladder (M3's shape scale) themes resolve to concrete radii, so
/// SHAPE is theme-driven: Photon returns its own radii, Material returns the M3 shape values, a brand
/// theme returns whatever it likes. Components request a role, never a hardcoded dp.
/// </summary>
public enum ShapeScale : byte
{
    None = 0,
    ExtraSmall = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
    ExtraLarge = 5,
    /// <summary>Fully rounded (pills, avatars) — engine-clamped to min(w,h)/2.</summary>
    Full = 6,
}
