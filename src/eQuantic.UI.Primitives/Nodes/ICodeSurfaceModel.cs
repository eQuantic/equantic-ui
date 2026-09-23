namespace eQuantic.UI.Primitives;

/// <summary>
/// The editing engine behind a <see cref="CodeSurface"/>, seen from a realizer: the whole of what a
/// host may TELL it and what it may ASK it.
/// <para>
/// A realizer does two things with a code surface and nothing else. It hands over what the platform
/// reported — a key, some text, a pointer — and it paints what the model answers: the carets and the
/// selection, as rectangles in the surface's own coordinates. It never turns a column into pixels and
/// never decides what a click means. Both used to be decided twice, once in each host, and the two
/// copies had drifted: the browser could not drag a selection or shift-click, the window could not
/// shift-click either, and each did its own arithmetic from line and column to pixels
/// (docs/CODE-EDITOR-PLAN.md, "the shape", §1).
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> <c>EditableText</c> owns the editing protocol,
/// <c>RenderEditable</c> paints what it is told, and <c>TextInputClient</c> is the narrow door the
/// platform talks through. This interface is that door and that paint order in one place, because a
/// realizer here is both the platform bridge and the render object. The engine that implements it
/// (<c>eQuantic.UI.Code</c>) knows nothing about any host; this assembly knows nothing about the
/// engine.
/// </para>
/// </summary>
public interface ICodeSurfaceModel
{
    /// <summary>
    /// The selection to paint: one band per line each selected range covers, in the surface's own
    /// coordinates. Empty when nothing is selected. A single rectangle over a multi-line range would
    /// cover the indentation of lines the range never touched, which is why these are per line.
    /// </summary>
    IReadOnlyList<Rect> SelectionBands { get; }

    /// <summary>Every caret, the primary first, in the surface's own coordinates.</summary>
    IReadOnlyList<Rect> Carets { get; }

    /// <summary>
    /// A key the platform reported, by NAME ("ArrowLeft", "Enter", "Tab", "z"). Answers whether the
    /// editor CLAIMED it: false leaves the key to whatever is around the editor — Escape to the
    /// dialog it sits in, Tab to the form when nothing can be indented.
    /// </summary>
    bool HandleKey(string key, KeyModifiers modifiers, ITextClipboard? clipboard);

    /// <summary>
    /// Text the platform decided the user typed — one character, a dead key's result, an input
    /// method's commit. It arrives as a STRING because what a keystroke produces is the platform's
    /// business: "á" may be one key or three. Answers whether the document took it.
    /// </summary>
    bool HandleText(string text);

    /// <summary>
    /// A pointer over the surface, at <paramref name="position"/> in the surface's own coordinates.
    /// <paramref name="clicks"/> is the platform's click count on a <see cref="PointerPhase.Down"/> —
    /// its double-click interval is a system setting, never the engine's to guess. Answers whether
    /// the model changed.
    /// </summary>
    bool HandlePointer(PointerPhase phase, Point position, KeyModifiers modifiers, int clicks);
}
