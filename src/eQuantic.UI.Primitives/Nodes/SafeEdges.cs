namespace eQuantic.UI.Primitives;

/// <summary>Which edges a <see cref="SafeArea"/> keeps clear. Flags, because a screen usually keeps
/// the top and bottom clear while letting content run edge to edge horizontally.</summary>
[Flags]
public enum SafeEdges : byte
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Start = 4,
    End = 8,
    /// <summary>
    /// The corner the SYSTEM'S WINDOW CONTROLS float in, under a unified desktop chrome — the
    /// close/minimise/zoom trio on a Mac at the start, the caption buttons on Windows at the end.
    /// A toolbar that IS the title bar asks for this and keeps clear of them on whichever side the
    /// platform put them, without knowing which. Not part of <see cref="All"/>: only the row that
    /// lives in the strip wants it, and the host reports zero everywhere the window has a bar of
    /// its own — a phone, the browser, a standard-chrome window.
    /// </summary>
    WindowControls = 16,
    Horizontal = Start | End,
    Vertical = Top | Bottom,
    All = Top | Bottom | Start | End,
}
