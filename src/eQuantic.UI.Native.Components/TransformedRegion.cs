using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// A region registered under a transform (a box's <c>BoxStyle.Transform</c>, a state's, a hover
/// lift): what turns a point on screen back into the space the region was laid out in. The region
/// itself holds the box its rect is drawn in, which is exact for a translation, a scale and a quarter
/// turn; a rotation or a shear draws a tilted shape inside that box, and a point is tested against
/// the shape through this (#513).
/// </summary>
/// <param name="Regions">The frame's list the region is in.</param>
/// <param name="Index">Where in that list.</param>
/// <param name="Inverse">From the screen back to the region's own space.</param>
/// <param name="Local">The region's rect in its own space.</param>
/// <param name="LocalDrawn">A hit region's drawn box in its own space, and the rect again for every
/// other kind.</param>
/// <param name="LocalOffered">What a code surface offers at its caret, in the surface's own space: a
/// turned list is tested against its own shape too, or the corners of the box around it took the
/// presses aimed at the code drawn there.</param>
internal readonly record struct TransformedRegion(object Regions, int Index, Matrix2D Inverse, Rect Local,
    Rect LocalDrawn, Rect? LocalOffered = null);
