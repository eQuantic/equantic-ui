using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The single source of the curated glyph PATH DATA (spec A10): one 24×24 single-path alpha mask per
/// <see cref="Icons"/> member (Material-style geometry). The web realizer emits it as inline
/// <c>&lt;svg&gt;&lt;path&gt;</c> with <c>fill="currentColor"</c>; the TS lowering consumes the SAME
/// data through the generated <c>icons.generated.ts</c> (byte-pinned — client path data is never
/// hand-written). The native atlas (W4) will rasterize from this registry too.
/// </summary>
public static class IconRegistry
{
    public static string Path(Icons glyph) => glyph switch
    {
        Icons.Search => "M15.5 14h-.79l-.28-.27a6.5 6.5 0 1 0-.7.7l.27.28v.79l5 4.99L20.49 19zm-6 0A4.5 4.5 0 1 1 14 9.5 4.5 4.5 0 0 1 9.5 14",
        Icons.Close => "M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z",
        Icons.Check => "M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z",
        Icons.CheckCircle => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2m-2 15-5-5 1.41-1.41L10 14.17l7.59-7.58L19 8z",
        Icons.Info => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2m1 15h-2v-6h2zm0-8h-2V7h2z",
        Icons.Warning => "M1 21h22L12 2zm12-3h-2v-2h2zm0-4h-2v-4h2z",
        Icons.Error => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2m1 15h-2v-2h2zm0-4h-2V7h2z",
        Icons.Person => "M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4m0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4",
        Icons.ChevronLeft => "M15.41 7.41 14 6l-6 6 6 6 1.41-1.41L10.83 12z",
        Icons.ChevronRight => "M10 6 8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z",
        Icons.ChevronUp => "m12 8-6 6 1.41 1.41L12 10.83l4.59 4.58L18 14z",
        Icons.ChevronDown => "m16.59 8.59-4.59 4.58-4.59-4.58L6 10l6 6 6-6z",
        Icons.Mail => "M20 4H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2m0 4-8 5-8-5V6l8 5 8-5z",
        Icons.Notifications => "M12 22a2 2 0 0 0 2-2h-4a2 2 0 0 0 2 2m6-6v-5c0-3.07-1.63-5.64-4.5-6.32V4a1.5 1.5 0 0 0-3 0v.68C7.64 5.36 6 7.92 6 11v5l-2 2v1h16v-1z",
        Icons.Heart => "m12 21.35-1.45-1.32C5.4 15.36 2 12.28 2 8.5A5.45 5.45 0 0 1 7.5 3 5.9 5.9 0 0 1 12 5.09 5.9 5.9 0 0 1 16.5 3 5.45 5.45 0 0 1 22 8.5c0 3.78-3.4 6.86-8.55 11.54zM12 18.65C16.86 14.24 20 11.39 20 8.5A3.45 3.45 0 0 0 16.5 5c-1.54 0-3.04.99-3.56 2.36h-1.87C10.54 5.99 9.04 5 7.5 5A3.45 3.45 0 0 0 4 8.5c0 2.89 3.14 5.74 8 10.15z",
        _ => "m12 21.35-1.45-1.32C5.4 15.36 2 12.28 2 8.5A5.45 5.45 0 0 1 7.5 3 5.9 5.9 0 0 1 12 5.09 5.9 5.9 0 0 1 16.5 3 5.45 5.45 0 0 1 22 8.5c0 3.78-3.4 6.86-8.55 11.54z", // HeartFilled
    };
}
