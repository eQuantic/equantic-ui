namespace eQuantic.UI.Primitives;

/// <summary>
/// The spreadsheet engine behind a <see cref="SheetSurface"/>, seen from a realizer: the whole of what
/// a host may TELL it.
/// <para>
/// A realizer hands over what the platform reported (a key, some text, a composition, a pointer, the
/// clipboard's events, focus) and paints one thing of its own, the entry's carets, which have to blink:
/// the cells, the selection band, the active cell's ring, the cell's editor and the fill preview are drawn
/// by the component above the surface, the same pixels on every target. A realizer never turns a point
/// into a cell and never decides what a press or a key means.
/// </para>
/// <para>
/// Its text arrives through the platform's own text door, the one <see cref="ICodeSurfaceModel"/> takes,
/// and the door is the SAME at rest and while a cell is being edited: the first character typed into a
/// cell may be a dead key or the start of an input method's composition, and moving the keyboard to
/// another element in the middle of one cancels it.
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
    /// sheet CLAIMED it: false leaves the key to whatever is around the sheet: Escape outside an edit, and
    /// the Tab that follows it, to the host, which moves the keyboard on. The <paramref name="convention"/>
    /// says which keyboard tradition the host's users live in: inside an entry a word and a line's ends
    /// are reached by different chords in the two, and Excel's own date and time chords differ on a Mac
    /// (<see cref="KeyboardConvention"/>). <paramref name="clipboard"/> is null on a host whose clipboard
    /// arrives as events of its own (a browser's copy, cut and paste): the copy keys are then left
    /// unclaimed, for those events.
    /// </summary>
    bool HandleKey(string key, KeyModifiers modifiers, KeyboardConvention convention, ITextClipboard? clipboard);

    /// <summary>
    /// Text the platform decided the user typed: one character, a dead key's result, an input method's
    /// commit, an emoji. It arrives as a STRING because what a keystroke produces is the platform's
    /// business: "á" may be one key or three. A composition in flight is replaced by it. Answers whether
    /// the sheet took it.
    /// </summary>
    bool HandleText(string text);

    /// <summary>
    /// The composition an input method is building, shown in the cell's editor as it grows and replaced
    /// by the next one; <c>""</c> cancels it and leaves the entry as it was before it began. A composition
    /// that begins while no cell is being edited begins an entry, as typing does. The platform commits it
    /// through <see cref="HandleText"/>.
    /// </summary>
    bool SetComposition(string text);

    /// <summary>What a copy takes: the selected cells as tab-separated values, or, while a cell is being
    /// edited, the entry's selected text.</summary>
    string CopyText();

    /// <summary>A copy that also empties what it took. Answers the text for the clipboard.</summary>
    string Cut();

    /// <summary>Text from the clipboard: tab-separated values placed at the selection's top-left, or,
    /// while a cell is being edited, text inserted at the entry's caret. Answers whether anything
    /// changed.</summary>
    bool Paste(string text);

    /// <summary>The entry's carets, the primary first, in the surface's own coordinates, while the cell's
    /// editor holds them; none otherwise. What a realizer paints ON TOP of everything the surface's child
    /// drew, and blinks, as it does a code surface's.</summary>
    IReadOnlyList<Rect> Carets { get; }

    /// <summary>
    /// Where the platform's text input stands, in the surface's own coordinates: the primary caret while a
    /// cell is being edited, the active cell before. A realizer keeps its input there, so an input method
    /// opens its window at the text being composed rather than at the grid's corner.
    /// </summary>
    Rect InputBounds { get; }

    /// <summary>
    /// Changes whenever the surface should TAKE the keyboard back: the formula bar or the name box is done
    /// with it. A realizer that sees a new value gives the surface the keyboard, as a click on it would, and
    /// remembers the value per MODEL, counting one it has never seen from 0, as it does a code surface's.
    /// </summary>
    int FocusVersion { get; }

    /// <summary>The surface gained or lost the keyboard. Losing it cancels a composition and keeps the
    /// entry open, as Excel keeps it.</summary>
    void FocusChanged(bool focused);

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
