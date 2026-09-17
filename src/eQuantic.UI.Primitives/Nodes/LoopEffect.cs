namespace eQuantic.UI.Primitives;

/// <summary>Loop-motion effects (spec §06: animate transform &amp; opacity ONLY — these are all transform).</summary>
public enum LoopEffect : byte
{
    /// <summary>Horizontal translate loop; offsets are FRACTIONS OF THE NODE'S OWN WIDTH, so both
    /// realizers resolve them without knowing the parent (CSS translateX(%) has the same base).</summary>
    SlideX = 0,
}
