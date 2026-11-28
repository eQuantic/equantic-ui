using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a composited tile is (#1937), and so what <see cref="PatternTileCache"/> keeps it by: what was
/// composited, the content of a vector (<see cref="ArtworkFingerprint"/>) or the source a raster was
/// decoded from, its size in device pixels, and the colour that answered the artwork's
/// <c>currentColor</c>. Mode and contrast reach a vector's pixels only through that colour, and a
/// raster's only through the source the mode picked, so nothing else can make two tiles differ. A
/// tile's opacity is the draw's, not the texture's, and is not part of it.
/// </summary>
/// <param name="Artwork">The vector's content fingerprint; zero for a raster.</param>
/// <param name="Source">The raster's resolved source; null for a vector.</param>
/// <param name="Width">The tile's width in device pixels.</param>
/// <param name="Height">The tile's height in device pixels.</param>
/// <param name="Tint">The colour of the artwork's inherited shapes; default when it has none, or for a raster.</param>
internal readonly record struct PatternTileKey(UInt128 Artwork, string? Source, int Width, int Height, Color Tint);
