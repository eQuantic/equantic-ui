namespace eQuantic.UI.Primitives;

/// <summary>
/// The editing engine behind a <see cref="CodeSurface"/>, seen from a realizer: the whole of what a
/// host may TELL it and what it may ASK it.
/// <para>
/// A realizer does two things with a code surface and nothing else. It hands over what the platform
/// reported — a key, some text, a composition, a pointer, the clipboard's events, focus — and it
/// paints the one thing that has to blink: the carets, as rectangles in the surface's own
/// coordinates. Everything else a code surface shows — the selection, the active line, the matches —
/// is drawn by the component above it, in the code's own layers, so it is the same on every target
/// and under the text where it belongs. A realizer never turns a column into pixels and never decides
/// what a click means (docs/CODE-EDITOR-PLAN.md, "the shape", §1).
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
    /// <summary>Every caret, the primary first, in the surface's own coordinates — what a realizer
    /// paints ON TOP of everything the surface's child drew, and blinks.</summary>
    IReadOnlyList<Rect> Carets { get; }

    /// <summary>
    /// Changes whenever the primary caret should be brought into view — a key moved it, a command
    /// did, find stepped to a match. A realizer that sees a new value scrolls whatever contains the
    /// surface until the caret is inside it; one that sees the same value leaves the scroll alone,
    /// so a reader's own scrolling is never undone by a rebuild.
    /// </summary>
    int RevealVersion { get; }

    /// <summary>
    /// A key the platform reported, by NAME ("ArrowLeft", "Enter", "Tab", "z"). Answers whether the
    /// editor CLAIMED it: false leaves the key to whatever is around the editor — Escape to the
    /// dialog it sits in, Tab to the form once Escape has released it. The
    /// <paramref name="convention"/> says which keyboard tradition the host's users live in, because
    /// the same chord means different things in the two (<see cref="KeyboardConvention"/>).
    /// <paramref name="clipboard"/> is null on a host whose clipboard arrives as events of its own
    /// (a browser's copy, cut and paste): the copy keys are then left unclaimed, for those events.
    /// </summary>
    bool HandleKey(string key, KeyModifiers modifiers, KeyboardConvention convention, ITextClipboard? clipboard);

    /// <summary>
    /// Text the platform decided the user typed — one character, a dead key's result, an input
    /// method's commit. It arrives as a STRING because what a keystroke produces is the platform's
    /// business: "á" may be one key or three. A composition in flight is replaced by it. Answers
    /// whether the document took it.
    /// </summary>
    bool HandleText(string text);

    /// <summary>
    /// The composition an input method is building, shown in the document as it grows and replaced
    /// by the next one; <c>""</c> cancels it and leaves the document as it was before it began. The
    /// platform commits it through <see cref="HandleText"/>.
    /// </summary>
    bool SetComposition(string text);

    /// <summary>What a copy takes: the selection, or the caret's whole line when nothing is
    /// selected.</summary>
    string CopyText();

    /// <summary>A copy that also removes what it took. Answers the text for the clipboard.</summary>
    string Cut();

    /// <summary>Text from the clipboard, placed the way a paste places it.</summary>
    bool Paste(string text);

    /// <summary>
    /// A pointer over the surface, at <paramref name="position"/> in the surface's own coordinates.
    /// <paramref name="clicks"/> is the platform's click count on a <see cref="PointerPhase.Down"/> —
    /// its double-click interval is a system setting, never the engine's to guess. Answers whether
    /// the model changed.
    /// </summary>
    bool HandlePointer(PointerPhase phase, Point position, KeyModifiers modifiers, int clicks);

    /// <summary>The surface gained or lost the keyboard. Losing it ends a typing run and cancels a
    /// composition; gaining it traps Tab again.</summary>
    void FocusChanged(bool focused);
}
