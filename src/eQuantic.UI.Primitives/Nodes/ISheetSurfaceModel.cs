namespace eQuantic.UI.Primitives;

/// <summary>
/// The spreadsheet engine behind a <see cref="SheetSurface"/>, seen from a realizer: the whole of what
/// a host may TELL it.
/// <para>
/// A realizer hands over what the platform reported (a key, some text, a pointer, the clipboard's
/// events) and paints nothing of its own: the cells, the selection band, the active cell's ring, the
/// draft and the fill preview are drawn by the component above the surface, the same pixels on every
/// target. A realizer never turns a point into a cell and never decides what a press or a key means.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> As it solves a text field, which <see cref="ICodeSurfaceModel"/>
/// follows too: the widget owns the editing protocol and the platform talks through a narrow door.
/// This interface is that door. The engine that implements it (<c>eQuantic.UI.Sheet</c>) knows
/// nothing about any host; this assembly knows nothing about the engine.
/// </para>
/// </summary>
public interface ISheetSurfaceModel
{
    /// <summary>
    /// A key the platform reported, by NAME ("ArrowLeft", "Enter", "F2", "c"). Answers whether the
    /// sheet CLAIMED it: false leaves the key to whatever is around the sheet, Escape outside an edit
    /// to the host: Photon takes the keyboard back, and a browser leaves it where it is. No keyboard
    /// convention is handed, because Excel's chords are one keyboard in both traditions once
    /// <see cref="KeyModifiers.Command"/> stands for ⌘ or Ctrl. <paramref name="clipboard"/> is null on a host whose clipboard arrives as events of its
    /// own (a browser's copy, cut and paste): the copy keys are then left unclaimed, for those events.
    /// </summary>
    bool HandleKey(string key, KeyModifiers modifiers, ITextClipboard? clipboard);

    /// <summary>
    /// Text the platform decided the user typed. It arrives as a STRING because what a keystroke
    /// produces is the platform's business. Answers whether the sheet took it.
    /// </summary>
    bool HandleText(string text);

    /// <summary>What a copy takes: the selected cells, as tab-separated values.</summary>
    string CopyText();

    /// <summary>A copy that also empties what it took. Answers the text for the clipboard.</summary>
    string Cut();

    /// <summary>Tab-separated values from the clipboard, placed at the selection's top-left. Answers
    /// whether a cell changed.</summary>
    bool Paste(string text);

    /// <summary>
    /// A pointer over the surface, at <paramref name="position"/> in the surface's own coordinates.
    /// <paramref name="clicks"/> is the platform's click count on a <see cref="PointerPhase.Down"/>:
    /// its double-click interval is a system setting, never the engine's to guess. <paramref name="kind"/>
    /// is what pressed, which the engine reads as Flutter's drag devices do: a mouse's or a stylus's drag
    /// selects, a finger's pans the grid around the sheet, so a finger's press is a tap that lands when
    /// it lifts, and the host says <see cref="PointerPhase.Cancel"/> when a pan took it. A host routes the
    /// moves and the release of a press that began on the surface to it until the press lifts, wherever
    /// the pointer goes. Answers whether the model changed, or needs a frame to follow it.
    /// </summary>
    bool HandlePointer(PointerPhase phase, Point position, KeyModifiers modifiers, int clicks, PointerKind kind);

    /// <summary>
    /// What the pointer looks like resting at <paramref name="position"/>, in the surface's own
    /// coordinates: the sheet's answer, because only it knows what a press there would do (a crosshair
    /// where it would grab the fill handle). What the component draws over the cells takes no pointer on
    /// any target, so no drawn box can say it.
    /// </summary>
    PointerCursor CursorAt(Point position);
}
