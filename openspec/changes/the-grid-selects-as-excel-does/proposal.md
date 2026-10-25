# Proposal

Closes #984, slice 2a of the spreadsheet plan (`docs/SPREADSHEET-PLAN.md`), a sub-issue of the epic
#981 (a grid that stands beside Excel and Google Sheets). Slice 2 was split with Edgar on 2026-10-25:
the grid read aloud (D17), with the accessibility contract #1078 needs for a code surface, is slice
2b, #1081.

## Why

The grid selects one rectangle and moves through Excel's most common keys, and stops there. A header
is a button that hears no modifier, so Shift+click and a drag across headers cannot stretch a band
(D14); ⌘/Ctrl+click cannot add a range; Ctrl+End goes to the grid's last cell rather than the last one
in use (D10); Windows' Ctrl+Y does not redo (D13); and the row, column, region and page chords Excel
users reach for do nothing. The range is drawn as a fill over the cells with the active cell filled too,
it looks the same whether the grid holds the keyboard or not, and nothing sums what is selected. Every
later slice that acts on a selection (commands, the clipboard, formatting, formulas) acts on the
selection this slice completes.

## What Changes

- **Several ranges at once.** ⌘/Ctrl+click adds a cell and ⌘/Ctrl+drag a range to the selection;
  Shift+F8 enters Excel's add mode, where a press or a drag adds without the modifier, until Shift+F8 or
  Escape. The last range is the active one and holds the active cell. Copy, cut, paste, Ctrl+D, Ctrl+R
  and the fill handle act on the active range; Delete clears every range and Ctrl+Enter fills every
  range, as Excel does.
- **Headers answer the pointer through the engine.** A header is no longer a button: a press on a
  column header selects the column, a drag across headers stretches the band, Shift+click stretches it
  from the anchor's column, ⌘/Ctrl+click adds the column as a range; the same for rows, and the corner
  selects the sheet. The resize grips on the headers' edges keep working as they do.
- **Excel's chords.** Shift+Space selects the active cell's rows and Ctrl+Space its columns (⌃Space on
  a Mac); ⌘A (Ctrl+A) selects the current region around the active cell, then the whole sheet; ⌘/Ctrl
  +End goes to the last cell in use (D10) and ⌘/Ctrl+Shift+End stretches to it; Alt+PageDown and
  Alt+PageUp move by a screen of columns; Ctrl+Backspace (⌃Delete on a Mac) brings the active cell into
  view without moving it; Ctrl+. (⌃. on a Mac) moves the active cell to the active range's next corner;
  ⌘/Ctrl+Y redoes (D13).
- **The Tab-Enter column.** Enter after a run of Tabs goes down to the column the run began in, as a
  form is filled in Excel.
- **The range is one rectangle.** Each range is drawn as a translucent fill under one outline, and the
  active cell inside its range is left unfilled; a header in a selected band is emphasized, and strongly
  when its whole row or column is selected; while the grid does not hold the keyboard the selection is
  drawn dimmed.
- **The cell cursor glides** to where a key or a press moves it, through the theme's motion tokens, and
  jumps under reduced motion. The table's marks gain the glide, Flutter's `AnimatedPositioned` for a
  mark (`TableViewCell.Glide`), on both realizers.
- **A status bar** (`StatusBar(controller)`) joins the formula bar and the name box in
  `eQuantic.UI.Spreadsheets`, placed by the app: the Sum, the Average and the Count of what is selected,
  read in the page's culture until values are typed (slice 5), in English, Portuguese and Spanish.
- **What changes for a developer**: `SheetController.Ranges` lists every selected range, the active one
  last; `Selection` is the active range, and setting it leaves one range. `SelectRows` and `SelectCols`
  take the band's anchor and focus with an `add` flag; the sheet theme slot names the range's outline,
  the emphasized header and the dimmed selection.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `spreadsheet`: several ranges, header gestures through the engine, Excel's row, column, region, page
  and corner chords, Ctrl+End to the last cell in use, Ctrl+Y, the Tab-Enter column, the range drawn as
  one outlined rectangle with header emphasis and the dimmed selection, the gliding cursor, the status
  bar, and the theme slot's new colours.
- `table-viewport`: a mark may glide from where it stood to where it moves.

## Impact

- **eQuantic.UI.Sheet** (the engine): the selection becomes a list of ranges; header hit-testing and
  header gestures; the new chords in `SheetKeymap`; the current region and the last cell in use; the
  status bar's summary. Its twins are regenerated.
- **eQuantic.UI.Spreadsheets** (the components): headers drawn without a pressable, the marks drawn as
  outlined ranges, the cursor's glide, the `StatusBar` component and its factory.
- **eQuantic.UI.Primitives** (the vocabulary): `TableViewCell.Key` and `TableViewCell.Glide`; the
  sheet theme slot's new members, crossing the theme bridge.
- **The realizers**: the web's table lowering and Photon's table emit animate a keyed mark between
  rectangles and snap under reduced motion; no host protocol widens, since the press already carries its
  modifiers to the engine.
- **eQuantic.UI.Components**: `SdkStrings.Sum`, `Average`, `Count` and `AddToSelection` in English,
  Portuguese and Spanish.
- **The public surface moves** (`PublicAPI.Unshipped.txt` of Primitives, Sheet, Spreadsheets and
  Components); **the developer surface does not**. **BREAKING** in preview: `SelectRows(from, to)` and
  `SelectCols(from, to)` become `SelectRows(anchor, focus, add)` and `SelectCols(anchor, focus, add)`;
  migration: `controller.SelectRows(r, r)` is `controller.SelectRows(r, r, add: false)`. The headers
  are no longer `Pressable` nodes, so a test that found a header by its pressable finds it by the
  sheet's own hit-testing.
- **Docs**: `docs/SPREADSHEET-PLAN.md` (slice 2 split into 2a and 2b, 2a delivered), the wiki's
  Components page in five languages, a ledger entry.
