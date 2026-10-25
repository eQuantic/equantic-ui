# The spreadsheet — Excel's and Sheets' grid, written once

## Why a plan, and why now

`Spreadsheet` shipped in August as a v1 with its fences stated: the strings the user typed, no
formulas, no formatting, no merges, no frozen panes, vertical virtualization only. #949 (2026-10-17)
then gave its engine an assembly of its own, `eQuantic.UI.Sheet`, driven from both hosts through one
protocol (`ISheetSurfaceModel`), the road the code editor took. The foundation is sound: a sparse
document, Excel's keyboard in one shared keymap, undo by inverse edits, a fill handle, resize grips.

The bar is now stated by Edgar (2026-10-18): **a grid that stands beside Microsoft Excel and Google
Sheets** — every gesture, every visual indicator, every effect, every way to drag, select and resize
rows and columns, so that using it is complete and pleasant and never frustrates. This document is
that bar, what is broken against it today (measured before anything was changed), the shape that
reaches it, and the slices in order.

Four decisions taken with Edgar (2026-10-18) shape everything else:

- **The vocabulary gains a two-dimensional viewport with pinned bands**, realized on both targets,
  and the spreadsheet is rebuilt on it (§2). Headers, frozen panes and both scroll axes become one
  layout instead of code that keeps two scrollers in step.
- **The cell editor and the formula bar are built on the code editor's engine**, `eQuantic.UI.Code`
  (§4): its text door, caret, selection, undo, completion list and signature help, rather than a
  second text editor.
- **The formula engine is an opt-in package**, `eQuantic.UI.Sheet.Formulas` (§7). The base engine
  defines where a calculator plugs in and stays a grid of typed values without it.
- **Interaction comes first**: slices 0 to 4 (viewport, editor, selection, commands and structure,
  clipboard and moving cells) before typed values, formatting and formulas.

Four more came out of reviewing this plan with him the same day:

- **The grid is Excel's size**: 1 048 576 rows by 16 384 columns addressable, sparse, costing only
  what is built; an app may declare less (§2, D18).
- **A sheet asked to fill an axis with no bound takes the visible extent of the scroller around
  it** and scrolls inside, and a development log says what was decided (§2, D1).
- **The formula bar, the name box, the status bar and the toolbar are components of their own**
  over the same controller, placed by the app: the grid is only the grid (§4).
- **Slice 0 lands in two parts**: 0a, the viewport in the vocabulary on both realizers; 0b, the
  spreadsheet rebuilt on it.
- **The spreadsheet's components get an assembly of their own**, as the charts have one: the grid,
  the formula bar, the name box, the status bar and the toolbar leave `eQuantic.UI.Components` for a
  family assembly above it, which the SDK adds as it adds the others, and which the formula package
  builds on. The engine stays in `eQuantic.UI.Sheet`, below the components (§4).

## What it has to beat — the parity bar

The union of what Excel (desktop, Windows and Mac) and Google Sheets offer in the grid itself. "Now"
is what the component does on 2026-10-18; "further" is where the design is meant to be better than
both, not merely equal.

### The grid and its viewport

| capability | Excel | Sheets | now | slice | further |
|---|---|---|---|---|---|
| Rows and columns virtualized on both axes | ✓ | ✓ | rows only | 0 | |
| 1 048 576 rows by 16 384 columns, sparse | ✓ | rows added on demand | 131 071 rows at most D18 | 0 | |
| Headers that stay put while the cells scroll, aligned at any width | ✓ | ✓ | ✗ D3 | 0 | aligned by construction: one viewport, pinned bands |
| Horizontal scroll, Shift+wheel, trackpad on both axes | ✓ | ✓ | ✗ D4 | 0 | |
| The active cell kept in view by every move | ✓ | ✓ | ✗ D2 | 0 | |
| Autoscroll while a drag (select, fill, move) nears an edge | ✓ | ✓ | ✗ | 0 | speed follows the distance past the edge |
| Row header that widens with the row number's digits | ✓ | ✓ | fixed 44 | 0 | |
| Frozen rows and columns, divider draggable | ✓ | ✓ | ✗ | 8 | the same pinned bands the headers use |
| Zoom (⌘ wheel, pinch) | ✓ | ✓ | ✗ | 8 | |

### Selection and navigation

| capability | Excel | Sheets | now | slice | further |
|---|---|---|---|---|---|
| Click, drag, Shift+click, Shift+arrows, ⌘/Ctrl+Shift+arrows | ✓ | ✓ | ✓ | — | |
| Header click, header drag, Shift+click on headers | ✓ | ✓ | click only | 2a | |
| ⌘/Ctrl+click: several ranges at once | ✓ | ✓ | ✗ | 2a | |
| Shift+Space row, Ctrl+Space column; ⌘A current region, then the sheet | ✓ | ✓ | ⌘A sheet only | 2a | |
| PageUp/PageDown, Alt+PageUp/PageDown, Ctrl+Backspace (show the active cell) | ✓ | ✓ | ✗ | 2a | |
| Ctrl+End goes to the last USED cell | ✓ | ✓ | ✗ D10 | 2a | |
| Enter after a Tab run returns to the column the run began in | ✓ | ✓ | ✗ | 2a | |
| Go To (F5, Ctrl+G) and the name box | ✓ | ✓ | ✗ | 1 | |
| A range drawn as one outlined rectangle; header emphasis; dimmed when unfocused | ✓ | ✓ | per-cell fill | 2a | |
| The cell cursor glides between cells | ✓ (365) | — | ✗ | 2a | the theme's motion tokens, off under reduced motion |
| Status bar: Sum, Average, Count of the selection | ✓ | ✓ | ✗ | 2a | a component of its own, placed by the app |

### Editing a cell

| capability | Excel | Sheets | now | slice | further |
|---|---|---|---|---|---|
| Type to replace, F2 and double-click to edit in place, Escape cancels | ✓ | ✓ | ✓ | — | |
| Backspace clears the cell and begins editing; Delete clears | ✓ | Backspace clears | both clear | 1 | |
| Dead keys, IME, emoji, AltGr | ✓ | ✓ | ✗ D5 | 1 | the code surface's input door, not a second one |
| Caret anywhere, selection inside the cell, word moves, Home/End | ✓ | ✓ | ✗ D15 | 1 | |
| Enter mode and Edit mode (arrows commit, or move the caret; F2 toggles) | ✓ | ✓ | Enter mode only | 1 | |
| Line break in a cell (Alt+Enter / ⌥↩) | ✓ | ✓ | ✗ | 1 | |
| Ctrl+Enter fills the selection; Ctrl+; date, Ctrl+Shift+; time | ✓ | ✓ | ✗ | 1 | |
| The editor grows over its neighbours as the text grows | ✓ | ✓ | ✗ | 1 | |
| Formula bar, live with the cell editor | ✓ | ✓ | ✗ | 1 | one document behind both, so they cannot disagree; a component the app places |
| AutoComplete from the column's own values | ✓ | ✓ | ✗ | 5 | |

### Clipboard, fill and moving cells

| capability | Excel | Sheets | now | slice | further |
|---|---|---|---|---|---|
| Copy and paste as tab-separated values | ✓ | ✓ | ✓ | — | |
| HTML tables in and out (Word, Docs, web pages) | ✓ | ✓ | ✗ | 4 | |
| Marching ants on the copied range, Escape clears them | ✓ | ✓ | ✗ | 4 | |
| Cut marks the cells and the paste MOVES them | ✓ | ✓ | ✗ D9 | 4 | |
| One value pasted over a selection fills all of it; a block repeats over a multiple | ✓ | ✓ | ✗ | 4 | |
| Paste values only, paste transposed | ✓ | ✓ | ✗ | 4 | |
| Drag a selection's border to move it, ⌥/Ctrl to copy, Shift to insert | ✓ | ✓ | ✗ | 4 | |
| Fill handle: copy | ✓ | ✓ | ✓ | — | |
| Fill handle: series (1, 2 → 3; Mon → Tue; Jan → Feb; dates; "Item 1" → "Item 2") | ✓ | ✓ | ✗ | 5 | |
| Double-click the fill handle fills down to the neighbour column's data | ✓ | ✓ | ✗ | 5 | |
| Fill handle dragged back into the selection clears what it leaves | ✓ | — | ✗ | 5 | |
| ⌘D / ⌘R | ✓ | ✓ | ✓ | — | |

### Rows and columns

| capability | Excel | Sheets | now | slice | further |
|---|---|---|---|---|---|
| Resize by the separator | ✓ | ✓ | ✓ | — | |
| A guide line across the grid and the size in a tooltip while resizing | ✓ | ✓ | ✗ | 3 | |
| Resize every selected column at once | ✓ | ✓ | ✗ | 3 | |
| Double-click the separator fits the content | ✓ | ✓ | ✗ | 3 | |
| Insert and delete rows and columns: keys, menu, N at once | ✓ | ✓ | engine only D11 | 3 | |
| Hide and unhide, with the hidden spot marked | ✓ | ✓ | ✗ | 3 | |
| Reorder by dragging a selected header | — | ✓ | ✗ | 3 | |
| Context menus on cells, headers and the corner (right-click, Shift+F10, long press) | ✓ | ✓ | ✗ D11 | 3 | one command registry behind keys, menus and toolbars |

### Values, formatting, formulas, data

| capability | Excel | Sheets | now | slice | further |
|---|---|---|---|---|---|
| Typed values: number, text, boolean, date and time, error | ✓ | ✓ | text only D8 | 5 | parsed in the app's culture, through .NET's own |
| Alignment by type, long text spilling into empty neighbours, `###` for a narrow number | ✓ | ✓ | ✗ D7 | 5 | |
| Number formats (general, number, currency, percent, date, custom codes) | ✓ | ✓ | ✗ | 6 | |
| Cell styles: bold, italic, underline, colours, alignment, wrap, borders; ⌘B ⌘I ⌘U | ✓ | ✓ | ✗ | 6 | typed tokens, never a CSS string |
| Rows that grow with wrapped text; format painter | ✓ | ✓ | ✗ | 6 | |
| Formulas: references, ranges, `$` anchors, operators, a function catalogue | ✓ | ✓ | ✗ | 7 | an opt-in package; functions an app adds in C# |
| Incremental recalculation, circular references, `#REF!` and its siblings | ✓ | ✓ | ✗ | 7 | |
| References that follow inserts, deletes, fills and copies | ✓ | ✓ | ✗ | 7 | |
| Formula editing: coloured references on the grid, point mode, F4, function completion and argument hints | ✓ | ✓ | ✗ | 7 | the code editor's completion and signature help |
| Merged cells, hidden gridlines | ✓ | ✓ | ✗ | 8 | |
| Sort, filter from the header, find and replace | ✓ | ✓ | ✗ | 9 | |
| Data validation (lists, checkboxes), conditional formatting, notes, links | ✓ | ✓ | ✗ | 9 | |
| Several sheets, cross-sheet references, named ranges | ✓ | ✓ | ✗ | 10 | |
| CSV and XLSX in and out | ✓ | ✓ | TSV only | 10 | XLSX in an opt-in package |

### Every target

| capability | Excel | Sheets | now | slice | further |
|---|---|---|---|---|---|
| Touch: tap selects, a drag scrolls, a long press selects, handles stretch the range, double tap edits | ✓ | ✓ | ✗ D6 | 0–3 | |
| Screen readers: grid, rows, cells, the active cell and its value announced | ✓ | ✓ | ✗ D17 | 2b | |
| The keyboard can leave the grid (WCAG 2.1.2) | ✓ | ✓ | ✗ #732 | 1 | |
| One grid on the web and in a native window | — | — | ✓ | all | write-once: the same engine and the same tests on both |

## What is broken today — measured (2026-10-18)

Measured in Chromium on the dashboard's `/sheet` screen (`origin/main` at f6f1e375a), unless the line
says it was read in the code. Photon was not driven for this list; each slice measures its own lines
there before it changes them.

| # | what | evidence |
|---|---|---|
| D1 | Inside the dashboard's scrolling page, `Height = Fill` lays the sheet out at its full content height: the sheet's own scroll never engages, its column headers scroll away with the page, and every row is built. | The sheet's scroller measured 5600 px for 200 rows of 28; after a page scroll of 1200 px the "A" header sat at y = −1043; all 200 rows were in the DOM. |
| D2 | Nothing keeps the active cell in view. | Forty ↓ from A1 left the active cell at y = 1303 in a 900 px window; nothing scrolled. `Spreadsheet` never writes its `ScrollView`'s offset. |
| D3 | In a window narrower than the grid, the column headers shrink and drift from their columns. | At 900 px, header B starts at x = 375 and column B at 401; each header advances about 70 px instead of 96, so H's header sits at 793 and H's cells at 977. #613 fixed the row-number strip only. |
| D4 | There is no horizontal scroll: the scroller hides the x overflow and the frame clips. | At 900 px, columns G and H (x 881–1073) were past the window, reachable by no pointer. The default `SheetController()` is 26 columns, 2496 px wide. |
| D5 | Text arrives only through `keydown` on the web: a dead key, an IME composition, an emoji picker or AltGr never reach the cell. | Read in `lowerSheetSurface`. The code surface had the same door, measured broken in September, and slice 1 of `CODE-EDITOR-PLAN.md` replaced it with the textarea, `beforeinput` and composition door; the sheet never moved to it. |
| D6 | The pointer is the mouse only on the web: no pointer or touch events, so on a touchscreen a drag scrolls the page and a range or a fill can never start. | Read in `lowerSheetSurface` (`mousedown`, document `mousemove`/`mouseup`). |
| D7 | Long text is cut with an ellipsis inside its cell instead of spilling into the empty neighbours. | Sixty characters typed in D1 showed "A long sent…" with E1 empty. |
| D8 | Every value is text: numbers sit on the left, and the sample's `7,01` is a string. | `1234567` typed in C1 rendered left-aligned (`text-align: start`). |
| D9 | Cut empties the cells at once; Excel and Sheets mark them and move them when the paste lands. | Read in `SheetKeymap` and `SheetController.Cut`. |
| D10 | Ctrl+End goes to the grid's last cell (row 1000, column Z of an empty default sheet), not to the last cell that holds something. | Read in `SheetController.Move` (`SheetMotion.SheetBoundary`). |
| D11 | Right-click opens the browser's own menu, and inserting or deleting rows and columns has no key, menu or gesture although the engine does both. | Read: no `contextmenu` handler; `Insert`/`Delete` have no caller outside tests. |
| D12 | The keyboard cannot leave a sheet: Tab is always taken (#732). | #732. |
| D13 | Redo has no Ctrl+Y, Windows' own chord. | Read in `SheetKeymap`: only ⌘⇧Z. |
| D14 | A header takes no modifiers, so Shift+click and a drag across headers cannot stretch a band. | Read: the header is a `Pressable` whose callback carries none. |
| D15 | The caret is always at the draft's end, and every arrow commits. | Read in `SheetController` (`Draft += text`, `EraseFromDraft`) and `SheetKeymap`. |
| D16 | The fill only tiles, the fill handle does nothing on a double-click, and a separator does nothing on a double-click. | Read in `SheetController.Fill` and `Spreadsheet`. |
| D17 | The cells are mute to a screen reader: the surface consumes its subtree, so the active cell and its value are never announced. | The v1 semantics, recorded in the spreadsheet's history. |
| D18 | The grid cannot address a row past 131 071: `CellRef.Key` packs `row × 16384 + column` into an `int`, which overflows at row 131 072 in .NET and not in JavaScript, so the two targets disagree past it. Excel has 1 048 576 rows. | Measured with `dotnet fsi`: row 131 072 packs to −2 147 483 648 and unpacks to row −131 072; row 1 048 575 unpacks to row −1. |

## The shape, decided

### 1. The engine decides, the hosts report

Unchanged from #949, and the rule every slice keeps: `eQuantic.UI.Sheet` owns what a key, a press,
a drag and a paste MEAN; a realizer reports the platform's events through `ISheetSurfaceModel` and
decides nothing. Every new gesture widens that protocol (the pointer's kind, so a touch drag can
scroll where a mouse drag selects; a context request; a composition), and both hosts implement the
widening in the same pull request.

### 2. One viewport, two axes, pinned bands

*How does Flutter solve it?* With `TableView` (`two_dimensional_scrollables`), built on
`TwoDimensionalScrollView`: one scroll position per axis, cells built lazily for the visible
rectangle only, and a number of leading rows and columns PINNED. The column headers are the pinned
first row, the row numbers the pinned first column, and frozen panes are simply more pinned rows and
columns. Headers and cells then share one layout and one scroll owner, so D3 cannot happen, D4 is
the second axis, and freezing panes later costs a count.

The vocabulary gains that viewport (a two-dimensional lazy viewport with pinned bands, its offsets
out on both axes and a way IN to reveal a rectangle), realized on both targets. `Spreadsheet`
is rebuilt on it, and `DataTable` can be later. Revealing a rectangle is what fixes D2, and what
autoscroll during a drag drives.

The grid is Excel's size, 1 048 576 rows by 16 384 columns, and costs only what is built; an app may
declare less. A cell's key becomes a `long`, the same number on both targets (D18).

A sheet asked to fill an axis that has no bound, which is what a sheet in a scrolling page is (D1),
takes the visible extent of the scroller around it on that axis and scrolls inside it, and a
development log says what was decided. Flutter's answer is an error ("Vertical viewport was given
unbounded height"); the product principle's is that the SDK already knows, so the app learns nothing.

### 3. Overlays live in the grid's own coordinates

The marks that are not a cell — the range's outline, the copied range's marching ants, the fill
preview, the resize guide, the moving ghost, frozen-pane dividers, coloured formula references —
are drawn in one layer over the cells, in the content's coordinates, from rectangles the engine
computes. They are never painted by restyling cells (today's selection is a per-cell background),
which is what lets a range be one outlined rectangle and lets the cell cursor glide.

Every colour and stroke the grid draws (gridlines, headers, the range's fill and outline, the ants,
the guides) comes from the spreadsheet's own theme slot, the per-component slots of
`ELEMENT-TREE-PLAN.md`, with defaults derived from the app's theme, so a rebrand reaches the grid
with no line of sheet code.

### 4. The cell editor is a text editor, not a string

The draft becomes a real text document with a caret, a selection, word moves, line breaks and
undo, edited through the platform's own text door (D5, D15). The code editor already has all of
it: the engine (`eQuantic.UI.Code`), the web door that composition and dead keys need, the
completion list, and signature help on its plan. The cell editor and the formula bar are two views
of ONE such document, so they stay in step as Excel's do. The formula package brings a formula
language for that document, which is how the coloured references and the function completion of
slice 7 arrive without a line of editor code.

The formula bar and the name box are components of their own, built over the sheet's controller and
placed by the app, as are the status bar and the toolbar of later slices: the grid is only the grid,
and an app that wants Excel's layout composes it, in the Flutter way.

They live together in an assembly of their own, above `eQuantic.UI.Components`, as
`eQuantic.UI.Charts` does for the charts: the code editor gave its ENGINE an assembly and kept its
components in `Components`, and the spreadsheet goes one step further because its family is larger
and an opt-in package builds on it. The engine stays where #949 put it, so a headless consumer (a
server filling a sheet, an import) reaches the document and its commands without a component. The
generic two-dimensional `TableView` of slice 0a is not the spreadsheet's: it stays in
`eQuantic.UI.Components`, where a data table can use it too.

### 5. Values are typed, and the culture is .NET's

A cell keeps what the user typed AND the value it means: a number, text, a boolean, a date or an
error. Typed input is read in the app's culture: its decimal and group separators and its group sizes
come from .NET's `NumberFormatInfo`, never from a table of our own. The grouping rule is Excel's, not
.NET's default parse: a group separator is accepted only where it separates whole groups of the
culture's sizes. `NumberStyles.Number` alone reads `7,01` as 701 in `en-US` and `7.01` as 701 in
`pt-BR` (measured; `NumberTextConformanceTests` pins the same for `1,5` in the invariant culture),
where Excel reads both as text. So `1,000` is a thousand and `7,01` is text in `en-US`, `7,01` is a
number and `7.01` is text in `pt-BR`, and slice 5 pins each of these against both targets. Display follows the value
(alignment, `###`, spilling text) and, from slice 6, a format.

### 6. Commands are data

Every action is a named command: what a key chord runs, what a menu item runs, what a toolbar
button runs. The keymap is a table of chords to commands, per keyboard tradition (Excel on
Windows, Excel on a Mac), that an app may extend or rebind in C#. Context menus are built from the
same registry, so a menu can never offer what the keyboard cannot do, or the reverse.

The traditions are Excel's, on Windows and on a Mac. Sheets' chords join where Excel has none (⌘⇧V
pastes values), and where the two disagree Excel wins: Backspace clears the cell and begins editing,
as Excel does, where Sheets only clears it.

### 7. Formulas plug in

The base engine knows that a cell may hold an input that something else computes, and nothing
about how. `eQuantic.UI.Sheet.Formulas`, referenced by an app that wants to calculate (the one
`PackageReference` the product principle allows: opting in), plugs a calculator into that seam:
the parser, references and ranges with their `$` anchors, the dependency graph and its incremental
recalculation, errors and cycles, references that follow inserts, deletes, fills and copies, the
function catalogue as a registry an app adds to in C#, and the formula language the cell editor
reads. Without it, `=1+1` is the text a user typed, shown as typed.

### 8. The net

The code editor's net, applied to a grid: the engine's tests in xUnit, its transpiled pins, the
protocol tests that drive the same press, key and clipboard event through the Photon host and the
browser and compare the answers, goldens for every mark, and a budget that fails: a keystroke and a
scroll step at 100 000 rows by 100 columns, measured in a bench and never in the browser pane,
whose animation frames are throttled.

## Slices

Each slice lands on both targets, keyboard, pointer, touch and screen reader together for what it
adds: accessibility and touch are not a slice at the end.

| slice | issue | what | fixes |
|---|---|---|---|
| 0a | [#982](https://github.com/eQuantic/equantic-ui/issues/982) | The two-dimensional viewport with pinned bands in the vocabulary, on both realizers: lazy on both axes, offsets out, a rectangle revealed in, the unbounded-axis rule; the cell key a `long` | D1 (the rule), D18 |
| 0b | [#995](https://github.com/eQuantic/equantic-ui/issues/995) | `Spreadsheet` rebuilt on it, in the family's own assembly: pinned headers, reveal and autoscroll, the overlay layer, the widening row numbers, its theme slot, touch scrolling on the web | D1–D4, D6 (scroll by touch) |
| 1 | [#983](https://github.com/eQuantic/equantic-ui/issues/983) | The text door and the cell editor: the code surface's input door, caret and selection in the cell, Enter and Edit modes, line breaks, the editor growing over its neighbours, the formula bar and the name box as components of their own, Go To, Ctrl+Enter, the date and time chords, Backspace that clears and edits, Tab released by Escape | D5, D12, D15 |
| 2a | [#984](https://github.com/eQuantic/equantic-ui/issues/984) | Selection and navigation complete: several ranges, header drags and Shift+clicks, row and column chords, the page chords, Ctrl+End, the Tab-Enter column; the range as one outlined rectangle, header emphasis, unfocused dimming, the gliding cursor; the status bar component | D10, D13, D14 |
| 2b | [#1081](https://github.com/eQuantic/equantic-ui/issues/1081) | The grid read aloud: grid semantics and announcements, with the accessibility contract a code surface needs (#1078) | D17 |
| 3 | [#985](https://github.com/eQuantic/equantic-ui/issues/985) | Commands and structure: the command registry and the keymap as data; context menus; insert and delete; hide and unhide; resize several at once, the guide line and its tooltip, fit to content; reorder by header drag; long press on touch | D11, D16 (fit) |
| 4 | [#986](https://github.com/eQuantic/equantic-ui/issues/986) | Clipboard and moving cells: HTML in and out, marching ants, cut that moves on paste, a paste that fills the selection, paste values and transposed, dragging a selection's border to move, copy or insert | D9 |
| 5 | [#987](https://github.com/eQuantic/equantic-ui/issues/987) | Typed values: the value model, parsing in the app's culture, alignment, spilling text, `###`; fill series, the double-clicked fill handle, the fill dragged back; AutoComplete | D7, D8, D16 (fill) |
| 6 | [#988](https://github.com/eQuantic/equantic-ui/issues/988) | Formatting: number formats, cell styles and their chords, wrapping rows, the format painter, a toolbar component | |
| 7 | [#989](https://github.com/eQuantic/equantic-ui/issues/989) | Formulas, in `eQuantic.UI.Sheet.Formulas`: the base engine's calculator seam; the parser, references and ranges, the catalogue, incremental recalculation, errors and cycles, references that follow structure; the formula editing experience | |
| 8 | [#990](https://github.com/eQuantic/equantic-ui/issues/990) | Frozen panes, merged cells, hidden gridlines, zoom | |
| 9 | [#991](https://github.com/eQuantic/equantic-ui/issues/991) | Data tools: sort, filter, find and replace, validation, conditional formatting, notes and links | |
| 10 | [#992](https://github.com/eQuantic/equantic-ui/issues/992) | The workbook: several sheets, cross-sheet references, named ranges; CSV and XLSX | |

Slices 0a, 0b and 1 come first because every other slice draws on them: an overlay, a reveal, a gesture
that scrolls, and a text door that types every character a user can type.

The track is [#981](https://github.com/eQuantic/equantic-ui/issues/981) on the board, each slice a
story under it. Two issues filed before the plan hang there too:
[#732](https://github.com/eQuantic/equantic-ui/issues/732) (D12, closed by slice 1) and
[#969](https://github.com/eQuantic/equantic-ui/issues/969) (a fill comparison the engine can write
as C# reads it once lifted `!=` compares values).

## Where the slices stand

Each slice proposes, implements and archives its change in its own pull request.

- **0a, delivered (2026-10-19, [#982](https://github.com/eQuantic/equantic-ui/issues/982)).** The
  vocabulary has a viewport on two axes, `TableViewport`, with the scroll model it carries
  (`ITableScrollModel`, implemented by `TableScroll`) and the extents of its rows and columns
  (`TableSpans`, `UniformTableSpans`, `SparseTableSpans`); the components have `TableView`, which
  builds the window with hysteresis. Both realizers draw it in four pinned bands with its marks and
  scrollbars, and own its offset, because Chromium draws an element at most 16 777 214 px tall
  (measured), which Excel's rows pass. Every shell reads both axes of the wheel, a finger pans and
  flings it, and an axis with no bound takes the visible extent of the scroller around it, with a
  development log (D1's rule). The cell key is a `long`, so D18 is closed. Proved in Chromium on the
  dashboard's `/table` (the last cell, `XFD1048576`, reached by both thumbs; the pinned bands
  aligned at 400, 900 and 1 440 px) and on Photon in the gallery's Table section. Benched over the
  whole sheet: a step inside the built cells costs 1.2 ms on Photon and 0.7 ms in Chromium, a
  rebuild of the window 14.6 ms and 14 ms. The spreadsheet itself still draws as before: it moves
  onto the viewport in 0b.
- **0b, delivered (2026-10-22, [#995](https://github.com/eQuantic/equantic-ui/issues/995)).** The
  spreadsheet has an assembly of its own, `eQuantic.UI.Spreadsheets`, which the SDK adds as it adds the
  charts. `Spreadsheet` is a `SheetSurface` over a `TableView` whose pinned first row is the column
  headers and pinned first column the row numbers, measured from the widest number in view, so a cell
  starts under its header by construction and only the cells in view are built, on both axes (D1, D3,
  D4 closed). The engine reads a press against the viewport's live scroll through the document's axes
  as `TableSpans`; a key, a paste, a fill, an undo and a redo raise a reveal version that carries the
  cell the command left, PageUp and PageDown move by the rows shown (D2 closed); a held drag hands its
  position to the scroll, which scrolls toward it while the engine follows the grid. The selection, the
  active ring, the fill's preview and its handle are marks above the cells, the fill handle's crosshair
  is the engine's answer, and the colours come from the theme's sheet slot (`SheetTheme`). The web
  surface takes pointer events: a mouse's and a pen's press is captured, a finger's is a tap on release
  or the table's pan (D6's scroll closed; a fill by touch waits for the long press of slice 3). Proved
  in Chromium on the dashboard's `/sheet` (headers aligned to 0 px at 400, 900 and 1 440 px scrolled on
  both axes, forty ↓ and PageDown in view, a drag held below the grid scrolling and extending until the
  release) and on Photon in the gallery's Spreadsheet section, a sheet of 100 000 × 100, in a Metal
  window and headless. Benched at 100 000 × 100: a keystroke costs 2.6 ms on Photon and 9.1 ms in the
  runtime, a scroll step inside the built cells 2.2 ms and 0.3 ms. Found on the way and fixed: a reveal
  request read from the active cell chased every cell a drag reached; the web kept a press in one
  render's closure and lost its release; the cells' padding was written in the wrong order since the
  first spreadsheet; a `Draggable` on the web added `pointer-events: auto` beside the `none` its child
  said.
- **1, delivered (2026-10-25, [#983](https://github.com/eQuantic/equantic-ui/issues/983)).** A cell's
  entry is the code engine's document, `SheetController.Entry`, in a language that pairs nothing: a caret
  anywhere, a selection by the mouse and by Shift, word moves in the keyboard's tradition, Home and End,
  undo while editing and the commit as one step of the sheet's undo (D15 closed). The sheet's surface
  takes the code surface's text door on both targets, one for its whole life because a composition dies
  when the keyboard moves: on the web a textarea at the active cell or the caret reading `beforeinput`,
  the composition events and the clipboard's, on Photon the marked text its shells already reported, so
  a dead key, an input method, an emoji and AltGr reach a cell and a composition shows in it as it grows
  (D5 closed). Enter and Edit modes as Excel has them, Alt+Enter's line break, Ctrl+Enter filling the
  selection, the date and time chords in the page's culture (⌃; and ⌘; on a Mac, which the web's door now
  tells apart), Backspace that clears and edits. The editor is a mark over the cells it covers, growing
  in whole columns and rows to the edges of the view, its text sliding past them. `FormulaBar` and
  `NameBox` join the family as components the app places, views of the same controller (they listen to
  its `Refreshed`): the bar is the entry's second view, the box shows the address or the size of a drag
  and goes to a cell, a range, rows or columns, on Ctrl+G (⌃G) and, on Photon, F5. Escape releases Tab
  (D12 and [#732](https://github.com/eQuantic/equantic-ui/issues/732) closed). The code engine places a
  proportional face when its grid carries a measurer, which the code editor never does. Proved in
  Chromium on the dashboard's `/sheet`, beside the formula bar and the name box (text typed as input, a
  composition, F2 with the arrows, Home and End, the bar edited and committed, Go To, the date and the
  time, Escape then Tab leaving the grid), on Photon through the macOS shell's own text input client,
  and the twins against .NET over a session of keys, text and compositions. Benched at 100 000 × 100: a
  character into an open entry costs 3.0 ms on Photon and 9.5 ms in the runtime. Found on the way and
  filed: a static field typed as an array of an enum is typed by the enum's name in its twin
  ([#1064](https://github.com/eQuantic/equantic-ui/issues/1064)), a field of a vocabulary struct with no
  zero is typed without the `undefined` it starts as
  ([#1065](https://github.com/eQuantic/equantic-ui/issues/1065)), and Photon lets Tab walk out of a
  modal layer ([#1066](https://github.com/eQuantic/equantic-ui/issues/1066)).
- **2a, delivered (2026-10-25, [#984](https://github.com/eQuantic/equantic-ui/issues/984)).** Slice 2
  was split with Edgar: the grid read aloud (D17) is 2b,
  [#1081](https://github.com/eQuantic/equantic-ui/issues/1081). The selection is a list of ranges
  (`SheetController.Ranges`, the active one last, which `Selection` is): a ⌘/Ctrl press or drag adds
  one, Shift+F8's add mode adds without the modifier, copy, cut, paste, the fill chords and the fill
  handle act on the active range and Delete and Ctrl+Enter on every range. The headers are plain boxes
  the engine reads with their modifiers (`SheetController.HeaderAt`): a press selects the band, a drag
  stretches it, Shift stretches from the anchor's band, ⌘/Ctrl adds it, and the resize grips stay
  draggable (D14 closed). Excel's chords in both keyboard traditions: Shift+Space, Ctrl+Space (⌃Space),
  ⌘A to the current region then the sheet, Ctrl+End to the last cell in use (D10 closed), Alt+PageDown
  and PageUp, Ctrl+Backspace, Ctrl+., Ctrl+Y (D13 closed), and the Tab-Enter column; the macOS shell
  now offers a key to the app before the input method while nothing is marked, as every other shell
  did, so Shift+Space selects there. Each range is drawn as one outlined rectangle with the active cell
  unfilled, a header is emphasized for its band and strongly for a whole row or column, the selection
  dims while the grid does not hold the keyboard, and the cell cursor glides (`TableViewCell.Key` and
  `Glide`) and jumps under reduced motion. `StatusBar(controller)` joins the formula bar and the name
  box: the Sum, the Average and the Count of the cells in use, as Excel's General writes them, in the page's language
  and culture. Found on the way and fixed here: the active cell sat at the end a drag or Shift moved,
  where Excel keeps it at the anchor; and a number could not be read in the page's culture in the
  browser (EQ2108), so `7,01` did not sum on a pt-BR page
  ([#1087](https://github.com/eQuantic/equantic-ui/issues/1087), with
  [#908](https://github.com/eQuantic/equantic-ui/issues/908)). Proved in Chromium on the dashboard's
  `/sheet` (headers, two ranges, the dimming, the glide and its reduced-motion jump, the Portuguese
  sums) and in a PhotonDesktop window driven through the macOS shell's input client (Shift+Space, a
  dead key and a Japanese composition in the sheet, a text field and the code editor, the status bar).
  Benched over Excel's 1 048 576 rows by 100 columns, the whole sheet selected and a hundred thousand cells
  in use: the summary costs 13 ms in .NET and 83 ms in the runtime (2.6 s and 10.8 s when it walked the
  selection), a key that moves nothing reads it from the cache, and a drag over a million cells in use
  reads what the range covers. Found and filed: a resize
  grip on the web takes the keyboard from the sheet
  ([#1092](https://github.com/eQuantic/equantic-ui/issues/1092)), and in the dashboard a status bar
  under the grid sits below the fold because the grid takes the scroller's whole extent
  ([#1006](https://github.com/eQuantic/equantic-ui/issues/1006)).
- **2b** ([#1081](https://github.com/eQuantic/equantic-ui/issues/1081)) is next.

## Fenced, on purpose

Real-time collaboration, pivot tables, charts drawn inside the grid (the chart components already
exist and can be fed from a range), macros, external data connections, printing and page layout.
Each is a product of its own; none is needed for the grid to stand beside Excel's and Sheets' in
everything a user touches.
