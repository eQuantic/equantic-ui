# Proposal

Closes #983, slice 1 of the spreadsheet plan (`docs/SPREADSHEET-PLAN.md`), a sub-issue of the epic
#981 (a grid that stands beside Excel and Google Sheets). It also closes #732.

## Why

A cell is edited through a string that only grows at its end. On the web the text reaches it only
through `keydown`, so a dead key, an input method, the emoji picker and AltGr type nothing (D5); on
both targets the caret is always at the draft's end and every arrow commits (D15); and the sheet takes
every Tab, so a keyboard cannot leave it (D12, #732, WCAG 2.1.2). Every later slice that types into a
cell (typed values, AutoComplete, formulas) needs a real text editor there first, and the plan decided
with Edgar that it is the code editor's engine, `eQuantic.UI.Code`, with the formula bar as a second
view of the same document.

## What Changes

- **The text door is the code surface's.** On the web the sheet's surface reads text through a
  textarea at the caret (`beforeinput`, the composition events, the clipboard's own events) and reads
  only commands from `keydown`, so a dead key, an input method, an emoji and AltGr reach the cell. A
  composition shows in the cell as it grows, underlined, on both targets: Photon hands a sheet the
  marked text its shells already report, which it dropped until now. The candidate window opens at the
  caret, or at the active cell before an edit begins.
- **The draft is a text document**, the code engine's: a caret anywhere, a selection inside the cell
  by the mouse and by Shift with the arrows, word moves in the keyboard's own tradition, Home and End,
  undo and redo while editing, and the commit as one step of the sheet's undo. A press inside the
  editor places the caret, a drag selects, a double click selects a word.
- **Enter mode and Edit mode**, as Excel has them: an entry begun by typing is in Enter mode, where an
  arrow commits and moves the active cell; F2, a double click and a press in the editor or the formula
  bar begin or switch to Edit mode, where the arrows move the caret; F2 toggles between the two.
- **The keys of an entry**: Alt+Enter (⌥↩) breaks the line inside the cell; ⌘↩ (Ctrl+Enter) fills the
  whole selection with the entry as one undo step; the date and time chords type today's date and the
  time in the page's culture (Ctrl+; and Ctrl+Shift+;, ⌃; and ⌘; on a Mac); Backspace clears the active
  cell and begins editing it, Delete clears the selection, as Excel does.
- **The editor grows over its neighbours** as the text grows, in whole columns to the right and whole
  rows down, up to the edges of the view, in the cell's type and alignment; past the right edge its
  text slides so the caret stays in view.
- **The formula bar** (`FormulaBar(controller)`) and **the name box** (`NameBox(controller)`) are
  components of their own in `eQuantic.UI.Spreadsheets`, placed by the app. The bar is a second view
  of the one document the cell editor shows, so the two cannot disagree keystroke by keystroke; a press
  in it edits the active cell from there. The name box shows the active cell's address, or the size
  of the range while a drag draws it (`5R x 3C`), and takes an address, a range, rows or columns to go
  to; the Go To chord (Ctrl+G, ⌃G on a Mac) takes the keyboard to it, and so does F5 on Photon,
  while in a browser F5 stays the browser's reload (decided with Edgar, 2026-10-25).
- **Escape releases Tab**: outside an edit, Escape leaves the next Tab to whatever is around the
  sheet, and any other key arms it again, as the code editor does. A keyboard can leave a sheet inside
  a dialog and outside one, on both targets.
- **The code engine places text it measures.** Its grid may carry a measurer, and then a caret, a
  selection band and a press are placed by the advance of the text in the view's own face instead of a
  monospaced pitch. The code editor itself stays monospaced; the cell editor, the formula bar and the
  name box draw in the sheet's proportional type.
- **BREAKING** (contract): `ISheetSurfaceModel` gains the code surface's text door (`SetComposition`,
  `Carets`, `InputBounds`, `FocusVersion`, `FocusChanged`) and its `HandleKey` takes the keyboard's
  convention; `SheetController.Draft`, `TypeIntoDraft` and `EraseFromDraft` go, replaced by the entry
  document (`SheetController.Entry`), and `BeginEdit` takes the mode it begins in. Migration: read the
  draft as `controller.Entry.Document.Text`, and a model of one's own implementing
  `ISheetSurfaceModel` adds the text door's members and the convention parameter.

## Capabilities

### New Capabilities

None. The spreadsheet's and the code editor's specs already exist and are widened.

### Modified Capabilities

- `spreadsheet`: text reaches a cell through the platform's text door on both targets; the entry is a
  text document with a caret, a selection and its own undo; Enter and Edit modes; the entry's keys
  (line break, fill the selection, date and time, Backspace that edits); the editor that grows over its
  neighbours; the formula bar and the name box with Go To; Escape releasing Tab; a press while editing
  and the clipboard while editing act on the entry's text.
- `code-editor`: the engine places a caret, a band and a press by measure when its grid carries a
  measurer.

## Impact

- **Vocabulary** (`eQuantic.UI.Primitives`, contract): `ISheetSurfaceModel` widened with the text
  door; `SheetSurface.CaretColor`; `SheetTheme.EntrySelection`. TypeScript twins (`nodes.ts`, the
  theme twin) follow.
- **Code engine** (`eQuantic.UI.Code`, contract): `CodeGrid` carries an optional measurer, and the
  controller's geometry (carets, bands, a press, the column a vertical move aims at) reads through it.
- **Sheet engine** (`eQuantic.UI.Sheet`, contract): references `eQuantic.UI.Code` now. The controller
  owns the entry (a `CodeEditorController`), the edit mode, the entry's two views and their geometry,
  the formula bar's and the name box's models, Go To, the fill of a selection with the entry, the date
  and time, and the released Tab; the keymap takes the convention.
- **Spreadsheets** (`eQuantic.UI.Spreadsheets`, contract): the cell editor drawn as a mark over the
  cells it covers, `FormulaBar`, `NameBox`, the shared drawing of an entry's lines, their factories in
  `SpreadsheetsUI`.
- **Web realizer and runtime**: the code surface's text door is shared by both surfaces
  (`lowerSheetSurface` takes it, its C# twin writes the same textarea); the sheet paints its carets and
  honours its focus requests as the code surface does.
- **Photon** (`PhotonHost`, emit and semantics): a sheet receives the marked text, paints and blinks
  its carets, anchors the input method's window at its input bounds and adopts its focus requests.
- **Samples**: the dashboard's `/sheet` and the PhotonDesktop gallery place the formula bar and the
  name box over the grid.
- **Developer surface**: no MSBuild property, configuration section or template parameter moves.
- **Docs**: `docs/SPREADSHEET-PLAN.md` and `docs/CODE-EDITOR-PLAN.md` (the proportional fence), the
  wiki's Components page in five languages, a ledger entry.
