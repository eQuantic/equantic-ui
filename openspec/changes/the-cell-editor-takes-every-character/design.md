# Design

## Context

The sheet's engine (`eQuantic.UI.Sheet`) edits a cell through `SheetController.Draft`, a string that
`TypeIntoDraft` appends to and `EraseFromDraft` shortens; `SheetKeymap` commits on every arrow; the
component draws the draft as a `Text` and a steady bar after it. On the web, `lowerSheetSurface` puts
`tabindex` on a `div` and reads characters from `keydown` (a key of length one with no command held).
On Photon, `PhotonHost.TextInput` hands a focused sheet the committed text, and `SetMarkedText`
records a composition for a sheet without telling its model, so nothing shows while an input method
composes.

The code editor solved each of these in its slice 1a (#368): the engine (`eQuantic.UI.Code`,
`CodeEditorController`) owns a document, a selection, a history and the composition; the web surface
reads text through a hidden textarea at the caret (`beforeinput`, the composition events, the
clipboard's events) and commands from `keydown`; Photon hands a code surface the marked text and paints
and blinks the carets the model answers. Its grid is monospaced by design (`CodeGrid`: an origin and a
cell, `CodeLineCells`: how many cells each text element takes), and `docs/CODE-EDITOR-PLAN.md` fences
proportional faces as "a different component". The cell editor is that component.

Constraints: the engine decides and the hosts report (plan §1); the vocabulary speaks no target's
language; one type per file; preview, so `ISheetSurfaceModel` and `SheetController` change shape
without a shim; the runtime budget fails when it grows without the commit saying so.

## Goals / Non-Goals

**Goals:**

- One text document behind the cell editor and the formula bar, the code engine's, so the two cannot
  disagree and slice 7's formula language, completion and signature help arrive on it without editor
  code.
- One text door per surface, written once per target and shared by the code surface and the sheet.
- The code engine places proportional text exactly, without touching what the monospaced editor does.
- The keyboard can leave a sheet (WCAG 2.1.2).

**Non-Goals:**

- Wrapping text inside the editor at the view's edge, and rows that grow with their lines: slice 6
  (wrapping rows). Until then the editor's text slides to keep the caret in view.
- Point mode (an arrow in a formula inserting a reference), coloured references and function
  completion: slice 7.
- AutoComplete from the column's values: slice 5.
- The grid's own semantics and announcements (D17): slice 2. The text door carries the sheet's name,
  as the `div` did.
- Excel's ✕ and ✓ buttons beside the formula bar, and an expandable bar: an app composes them from the
  controller's commands if it wants them; not in this slice.
- Defined names in the name box: slice 10 (named ranges).

## Decisions

### 1. The entry is a `CodeEditorController` the sheet owns

`SheetController.Entry` is a `CodeEditorController` over plain text, created with the controller and
emptied by every `BeginEdit`, its history cleared. `eQuantic.UI.Sheet` references `eQuantic.UI.Code`
(both reference only the vocabulary, and both are transpiled into the runtime, Code first). The commit
writes `Entry.Document.Text` into the active cell as one `SheetEdit`; Escape drops it.

`BeginEdit(string seed, SheetEditMode mode)` replaces `BeginEdit(string)`, with the caret at the seed's
end; `SheetEditMode` is `Enter` or `Edit`, readable as `EditMode`, flipped by `ToggleEditMode()` (F2).
`Draft`, `TypeIntoDraft` and `EraseFromDraft` go: their callers read `Entry.Document.Text`, and typing
goes through the entry.

*How does Flutter solve it?* A spreadsheet in Flutter is a `TableView` with an `EditableText` over the
cell, whose `TextEditingController` is the document (`docs/FLUTTER-PARITY.md`, the
`TextEditingController` + `EditableText` row). The code engine is our `TextEditingController` and more:
it already has the composition, text elements, word moves in both keyboard traditions, undo and the
clipboard.

*Alternatives.* A small text model of the sheet's own: a second editor to keep in step with the first,
which the plan rejected. The vocabulary's `TextEntry` (a native `<input>` on the web): the browser
would own the caret and the IME, but Photon's field is a different editor, the formula bar would be a
second document kept in step by events, and slice 7 could not put a formula language behind it.

### 2. The sheet's surface is the text door, not a code surface over the cell

The text door must be the SAME platform element at rest and while editing: the first character a
person types into a cell at rest may be a dead key or the start of a composition, and moving the focus
from one element to another in the middle of a composition cancels it in every browser and on macOS.
So the sheet surface keeps one door for its whole life, as Google Sheets keeps one input focused under
its grid, and the cell editor is drawn, not focused.

`ISheetSurfaceModel` therefore takes the code surface's door:

- `HandleKey(key, modifiers, convention, clipboard)`: the convention joins, because word moves and
  line ends differ between Apple's keyboards and the others inside an entry, and the date and time
  chords differ too (decision 6).
- `SetComposition(string)`: a composition at rest begins an empty entry in Enter mode first.
- `Carets` (the entry's carets while the cell view holds the caret, none otherwise), painted and blinked
  by the host over everything the child drew, as a code surface's.
- `InputBounds`: where the platform's input stands, the primary caret while editing and the active
  cell's rectangle at rest. The web places the textarea there and Photon answers the macOS input
  client's `firstRectForCharacterRange` with it, so the candidate window opens at the cell.
- `FocusVersion` and `FocusChanged(bool)`: the sheet asks for the keyboard back after the formula bar
  or the name box is done with it; losing it cancels a composition and keeps the entry open, as Excel
  does.

`SheetSurface` gains `CaretColor`, the ink the code surface already carries on its node, set by the
component from the sheet's palette.

*Web.* The door in `lowerCodeSurface` (the textarea, the `keydown` that carries only claimed
commands, `beforeinput`, the composition bookkeeping kept per model, the clipboard's events, focus and
blur) moves to one function both lowerings call, over the members the two models share. The sheet's
textarea takes the `tabindex` and the accessible name; the `div` keeps `role="grid"`. The `keydown`
fallback that typed `event.key` goes. The C# twin (`WebLoweringVisitor.LowerSheetSurface`) writes the
same textarea, so the component parity fixture keeps both sides at one shape. The browser's reload key
(F5, and ⌘R and Ctrl+R) is never handed to a sheet, as `browserPastesOn` already leaves the browser its
paste keys (decided with Edgar: F5 stays the browser's).

*Photon.* `SetMarkedText` hands a sheet target its composition as it hands a code target; the emit
paints the sheet's carets with the code surface's blink (`EmitCodeCarets` generalised over the two);
`CaretInFrame` reads a sheet region's `InputBounds`; the focus requests are adopted per model for
sheets as for code surfaces.

*Alternative.* A `CodeSurface` over the cell while editing, with the sheet surface keeping `keydown`
at rest: the first composed character at rest would still be lost (D5 stays half open), and a focus
move per edit would cancel compositions begun at rest.

### 3. Two views, one document: the active view's geometry is the entry's grid

The cell editor and the formula bar are views of `Entry`. `SheetController.EntryView` says which one
holds the caret (`Cell` or `FormulaBar`). Each view hands the controller its own geometry on every
build (the component that draws it does), and the controller assigns `Entry.Grid` from the active
view's, at the hand-over and whenever the active view changes. Only the active view asks the entry for
carets and bands; the other draws the text alone, as Excel's inactive view does. The formula bar's
model is `SheetController.FormulaBar`, an `ICodeSurfaceModel` (the bar is a `CodeSurface`): a press or
text there makes the bar the active view and begins an entry in Edit mode if none is open; Enter, Tab
and Escape do what they do in the cell and then ask for the sheet's keyboard (`FocusVersion`).

*Alternative.* Two `CodeEditorController`s kept in step through `Changed`: two documents, two
histories and two compositions, exactly what "one document" was decided against. A geometry passed
into every call of the code engine: a larger rewrite of an engine another session is actively
extending, for no behaviour the switch does not already give.

### 4. The code engine measures when its grid says how

`CodeGrid` gains an optional measurer, `Func<string, float>? Measure`, the width of a run of text in
the view's own face. The controller's geometry goes through two private helpers, the x of a column on a
line and the column at an x, which read the monospaced pitch as today when there is no measurer, and
the measured advance of the line's prefix at a text element boundary when there is one (a press lands
on the nearer boundary by a binary search over the elements, never inside one). The advances are kept
per line text, as `CellsOf` keeps a line's cells. The column a vertical move aims at becomes a distance
from the line's start, which a monospaced grid computes exactly as it did.

The component builds the measurer from the context (`text => context.MeasureText(text, style)`): the
context's measurer is the target's text measurer, not state of one build, so calling it at the next
key is sound. On the server it answers 0, where no cell is ever being edited.

*Why a delegate.* It is the narrowest seam that measures on demand: an array of advances handed at
build would be stale for a second key that arrives in the same frame as the first. `TableSpans` became
an abstract class because an interface in a constructor is read as a service to inject; a delegate on a
record is not.

### 5. The cell editor is a mark the engine sizes

While a cell is edited, the component adds one more mark over `SheetController.EditorRange`: the active
cell, widened to the right in whole columns while the entry's widest line (measured in the cell's type,
plus the cell's padding) needs more room, and down in whole rows while its lines do, never past the last
column and row in view. The mark draws the cell's background (hiding what it covers), the active
outline, and the entry's lines through `EntryLines`, a component of the family that draws a code
document's lines in a proportional type with the selection's bands under them and the composition
underlined (the formula bar and the name box draw with it too). When the widest line is wider than the
range at the view's edge, the engine slides the cell view's grid origin so the primary caret stays
inside it. Every cell keeps drawing as before, so the grid under the editor is untouched.

Marks take no pointer on either target (0b), so presses still reach the surface: the engine compares
a press with the editor's bounds while editing (decision 7). The active ring and the fill handle are
not drawn while editing; the editor's outline replaces the ring.

A cell's own `Text` loses its single line: a value with a line break shows the lines its row's height
holds, from the top.

### 6. The keymap: the entry's keys first, then the code engine's

`SheetKeymap.Handle` takes the convention. While editing, the sheet's own keys come first: Enter and
Tab commit and step; Escape cancels; F2 toggles the mode; in Enter mode ←→↑↓ with no modifier but Shift
commit and move; Alt+Enter inserts a line break into the entry; Command+Enter fills the selection
(`FillSelection`, one `SheetEdit` over every cell, the selection kept); the date and time chords insert
`Now()` written with the culture's `"d"` and `"t"` specifiers. Everything else goes to
`Entry.HandleKey(…, convention, clipboard)`, whose `KeyBindings` give back (`CodeCommand.None`) what a
cell has no use for: indent and outdent, line comments, the line moves, copies, deletes and inserts,
and the completion, hover, signature and problem keys until slice 7 wants some of them. Undo and redo
while editing are the entry's, and the branch returns before the sheet's own undo can be reached.

At rest: Backspace begins an empty entry in Enter mode; Delete clears the selection; F2 begins Edit
mode with the value; the date and time chords begin an entry with their text; the Go To chord (Ctrl+G;
⌃G under Apple's convention) and F5 ask the name box for the keyboard when one is mounted, and are left
unclaimed when none is.

The chords by convention: Windows and the others take Ctrl+; for the date and Ctrl+Shift+; for the
time (`KeyModifiers.Command`, which is Ctrl there); Apple's take ⌃; for the date (`KeyModifiers.Control`)
and ⌘; for the time, Excel for Mac's own. The key arrives as `;`, or `:` when Shift changes it on the
layout, and both are read.

The clock is `SheetController.Now`, a `Func<DateTime>` defaulting to `DateTime.Now`, so a test names
its moment. The culture is the page's on the web and the app's on Photon, which `runtime-dates` and
`page-culture` already make agree for the two specifiers.

### 7. A press while editing

`HandlePointer` while editing: a press inside the editor's bounds makes the cell view active, switches
to Edit mode and is forwarded to `Entry.HandlePointer` (the code engine places the caret, selects a word
on a double click and draws a selection on a drag), and its moves and release follow it there; a press
outside commits and is a press at rest. A double click at rest begins Edit mode and forwards the same
press to the entry, so the caret lands where the person clicked. `CursorAt` answers the text beam over
the editor.

### 8. The formula bar and the name box

`FormulaBar(controller)`: a box with the sheet's gridline border holding a horizontal `ScrollView` and a
`CodeSurface` whose child is `EntryLines` and whose model is `controller.FormulaBar`. At rest it draws
the active cell's value; it is as tall as the entry's lines, at least one. The surface's reveal keeps the
caret in the bar's view, as the code editor's does.

`NameBox(controller)`: a box with a `CodeSurface` over `controller.NameBox`, a `SheetNameBox` with a
one-line `CodeEditorController` of its own. Unfocused it shows the active cell's address, or `nR x mC`
while a drag of more than one cell is in flight (an `SdkStrings` format, so it follows the app's
language); focused it selects its text; Enter calls `SheetController.GoTo(text)`, which reads a cell, a
range, whole rows or whole columns, `$` anchors ignored, in either case (`SheetReference.TryParse`),
selects it, reveals its first cell and asks for the sheet's keyboard; a text that names nothing sets
`Invalid`, which the component draws with the theme's error colour. The component counts itself in
`OnMount` and out in `OnUnmount`, which is how the keymap knows a name box is there.

Both live in `eQuantic.UI.Spreadsheets` with their factories in `SpreadsheetsUI`; their models live in
the engine, so they are tested without a screen.

### 9. Escape releases Tab

`SheetController.TabReleased`, the code editor's `TabMovesFocus`: Escape at rest sets it and claims
nothing, any other key that is not a modifier clears it before anything else is decided, and Tab or
Shift+Tab with it set is left unclaimed. The hosts already move the focus for an unclaimed Tab (the
modal layer cycles only a Tab the control did not take, `modal-focus`), so nothing changes in them.
Photon ends the editing of a surface on an unclaimed Escape, as it does for a code surface; the tests
prove the next Tab still lands after the sheet.

## Risks / Trade-offs

- [The code engine is being extended by another session (lines, carets, history)] → the measured
  geometry touches only the geometry helpers and the aimed column; land it as its own commit early,
  merge `main` often, and keep the monospaced pins (`CodeCellsTests`, the editor's goldens, the
  transpiled pins) byte-identical as the proof it changed nothing there.
- [A measured caret disagrees with where the platform draws the glyphs (kerning, ligatures, shaping)]
  → the prefix is measured whole by the same measurer that lays the text out on each target, never by
  summing characters; a test on each target places a caret after `W` and after `ffi` and compares it to
  the drawn text's width.
- [The browser keeps the reload key and a sheet loses nothing it had] → F5 never reached the sheet on
  the web; only Photon gains it.
- [Text pasted while editing may hold tabs and line breaks] → inserted as-is, as Excel does; the cell's
  value is text, and the TSV codec already quotes both on the way out.
- [A rebuild per keystroke with an editor mark] → the 0b bench (a keystroke at 100 000 × 100) runs with
  an entry open and keeps its ceiling; the entry's lines are measured once per build.
- [Runtime size] → the formula bar, the name box and the entry's drawing join the runtime; the budget
  moves in the commit that adds them, with the number.
- [The door's accessible name] → the textarea carries the sheet's name, but a screen reader hears no
  cell yet; D17 stays slice 2's, as the plan orders it.

## Migration Plan

Contract breaks, in the pull request's migration notes and the wiki's Upgrading page:

- `SheetController.Draft` → `controller.Entry.Document.Text`; `TypeIntoDraft`, `EraseFromDraft` → the
  entry's own commands (`Entry.HandleText`, `Entry.Execute(CodeCommand.DeleteLeft)`).
- `SheetController.BeginEdit(seed)` → `BeginEdit(seed, SheetEditMode.Enter)` (typing) or
  `SheetEditMode.Edit` (F2).
- `ISheetSurfaceModel` → a model of one's own adds `SetComposition`, `Carets`, `InputBounds`,
  `FocusVersion`, `FocusChanged`, and the `KeyboardConvention` parameter of `HandleKey`.
- `CodeGrid` gains a parameter with a default; positional constructions keep compiling.

No rollback beyond reverting the pull request: nothing persists.
