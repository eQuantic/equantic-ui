using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Curated glyph path lookup — since the icon-pack refactor the DATA lives in
/// <see cref="CuratedIcons"/> (Primitives; target-neutral), and this registry just projects it for
/// the web-side consumers (the TS catalog generator and its pins).
/// </summary>
public static class IconRegistry
{
    public static string Path(Icons glyph) => CuratedIcons.Resolve(glyph).Path;
}
