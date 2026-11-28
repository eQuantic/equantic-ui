namespace eQuantic.UI.Native.Components;

/// <summary>What <see cref="PatternPainter"/> draws one pattern with (#1937): the paint walk's state, and
/// the areas of the box the pattern is placed in and clipped to.</summary>
/// <param name="State">The walk at the box whose pattern this is.</param>
/// <param name="Placement">The box's border, padding and content areas.</param>
internal readonly record struct PatternLayer(EmitState State, PatternPlacement Placement);
