# Design

## Context

The engine (`eQuantic.UI.Sheet`) decides what every key, press and drag means and the hosts report
them through `ISheetSurfaceModel` (plan, §1). Today the controller holds one `SheetRange`
(`_selection`) and the active cell; `SheetKeymap.HandleAtRest` answers the arrows, Home and End,
PageUp and PageDown, ⌘A (the sheet), ⌘Z and ⌘⇧Z, ⌘D and ⌘R. The component (`Spreadsheet`) draws the
headers as `Pressable` nodes whose callback carries no modifier (D14), and draws the selection as marks
over a `TableView`: a `RangeFill` band over the whole range (the active cell filled too) and the
active ring. A press on a header never reaches the engine: on Photon a pressable is hit before a sheet
region, and the web's sheet surface steps aside for anything with a button role (`notTheSheets`).

On every Photon shell but macOS a hardware key reaches the host before the platform's text input
(Windows' `WM_KEYDOWN` swallows its `WM_CHAR` when handled, iOS's `PressesBegan`, Android's
`DispatchKeyEvent`), as the browser's `keydown` precedes `beforeinput`. The macOS shell hands a key to
the input context first whenever a text target holds the caret and no ⌘ or ⌃ is held, so a sheet at
rest never hears Shift+Space: the input context inserts a space and an entry begins.

The marks of a `TableView` are `TableViewCell`s placed by index; neither realizer animates a mark that
moves. Photon has a `TransitionClock` that glides colours, shadows, sizes and transforms per element
under the platform's reduced-motion setting; the web glides CSS properties and its stylesheet already
answers `prefers-reduced-motion` for its other motion.

## Goals / Non-Goals

**Goals:**

- One selection model of several ranges that every command reads one way.
- Headers read by the engine with their modifiers, on both targets, with no host protocol widened.
- Excel's chords in both keyboard traditions, Shift+Space included on every shell.
- The range drawn as Excel and Sheets draw it, the cursor gliding, the status bar.

**Non-Goals:**

- The grid's semantics and its announcements, and a code surface's accessible value (slice 2b, #1081).
  Removing the headers' pressables leaves them announced as text until then.
- Resizing several rows or columns at once, fit to content, reordering by header drag (slice 3).
- Typed values: the status bar reads numbers from text until slice 5 types them.
- A refusal dialog for commands over several ranges: the active range answers them (decided with
  Edgar).

## Decisions

### 1. The selection is a list of ranges; `Selection` is the active one

`SheetController.Ranges` lists every range, the active one last, and `Selection` is that last range.
Setting `Selection`, a plain press, an arrow, Go To and every move that selects leave one range; a
⌘/Ctrl press and add mode append one. Copy, cut, paste and fill read `Selection`; `ClearSelection` and
`FillSelection` (Delete and Ctrl+Enter) walk `Ranges` inside one history step.

*Alternative:* a `SheetSelection` value type replacing `Selection`. It names the concept better, but
every caller of `Selection` (the bars, the fill, the clipboard, the tests) reads one rectangle, and the
active range is exactly that rectangle; a list beside it keeps those callers right without a wrapper
they would unwrap. Excel's object model does the same: `Selection.Areas` beside `ActiveCell`.

### 2. Headers are hit-tested by the engine

The engine already turns a point into a cell from the grid's geometry (`CellAt`). It gains `HeaderAt`:
the corner, a column header or a row header, from the pinned bands' sizes and the grid's offset. A press
in a header band runs the header's gesture (select the band, Shift stretches from the anchor's band, ⌘
adds it, a drag stretches it), and a finger's press selects when it lifts, as on a cell. The component
draws headers as plain boxes, so the press reaches the sheet region on Photon and passes
`notTheSheets` on the web; the resize grips stay `Draggable` nodes, which both hosts route before the
sheet (Photon checks drag regions first; the web skips `[data-eq-drag]`). `SelectRows` and `SelectCols`
take an anchor, a focus and `add`.

*How Flutter solves it:* `two_dimensional_scrollables`' `TableView` puts no gesture in its cells; its
spreadsheet sample wraps the table in one `GestureDetector` and maps the position to a cell or a header
itself. The engine is that detector here.

### 3. The keymap, in both traditions

| chord | Standard (Windows, Linux, web elsewhere) | Apple |
|---|---|---|
| rows of the active range | Shift+Space | Shift+Space |
| columns of the active range | Ctrl+Space | ⌃Space |
| current region, then the sheet | Ctrl+A | ⌘A |
| last cell in use, stretched | Ctrl+End, Ctrl+Shift+End | ⌘End, ⌘⇧End |
| a screen of columns | Alt+PageDown/PageUp | ⌥PageDown/PageUp |
| bring the active cell into view | Ctrl+Backspace | ⌃Delete (Backspace) |
| the range's next corner | Ctrl+. | ⌃. |
| redo | Ctrl+Y, Ctrl+Shift+Z | ⌘Y, ⌘⇧Z |
| add mode | Shift+F8 | Shift+F8 |

`KeyModifiers.Command` already stands for Ctrl in the Standard tradition, so most rows are one test;
the three that name ⌃ on a Mac read the convention, as Go To's ⌃G does.

### 4. The current region and the last cell in use come from the stored cells

The current region grows a rectangle from the active cell while any cell in use touches it on a side or
a corner, which is Excel's `CurrentRegion`, and reads only `HasValue` along the rectangle's border, so a
dense block costs its perimeter per step. The last cell in use is the largest row and the largest
column among the stored cells, found once per document version: `SheetDocument` gains a version that
every `SetCell` and every structural edit advances, which the status bar's summary keys on too.

### 5. The Tab-Enter column is a remembered column

A Tab that steps a single selected cell to the right remembers the column the run began in, unless a
run is already remembered; Enter moves to the next row in that column and forgets it; every other move
forgets it. It lives beside `Step`, so the commit path (Tab and Enter while editing) shares it.

### 6. The range drawn as one rectangle, from marks

Each range is drawn as an outline mark over its whole rectangle and as up to four fill marks around the
active cell (the bands above, below, left and right of it), so the active cell stays unfilled without
covering its value; a range that does not hold the active cell is one fill mark. The marks' colours
come from the sheet slot, which gains `RangeOutline`, `HeaderWholeBackground`, `HeaderWholeText`,
`DimmedRangeFill` and `DimmedOutline`, each defaulting to a foundation colour (the focus ring, the text
on it, a lighter surface highlight, the border), and crossing the theme bridge as the slot's other
members do. The dimmed colours apply while the model's `FocusChanged(false)` stands; a header reads the
ranges to pick no emphasis, the band's, or the whole row's or column's.

### 7. The cursor glides by a keyed mark: `TableViewCell.Key` and `Glide`

*How Flutter solves it:* `AnimatedPositioned` animates a child between two positions when its
parameters change, keyed by its place in the tree. A `TableViewCell` gains `Key` (identity across
builds) and `Glide` (a `MotionSpec?`). The web lowers a keyed mark as the same element across renders
and gives it a CSS transition on its left, top, width and height from the motion's duration and curve,
which the stylesheet's `prefers-reduced-motion` rule turns off; Photon's table emit keeps, per key, the
rectangle the mark stood over and when it began to move, interpolates the frame's rectangle by the
frame's time along the curve, reports the motion as active so the host asks for frames, and snaps under
reduced motion. The active ring is keyed `cursor` with `Motion.State`, the theme's base duration on its standard curve; the fill's preview, the ranges and
the editor do not glide.

*Alternative:* a `Position` channel on `TransitionSpec`. A mark is placed by its table indices, not by
a style, and a transition keyed on style would glide a cell that scrolls; the glide belongs to the mark.

### 8. The status bar reads a summary the engine keeps

`SheetController.Summary` answers the count of cells in use, the count of numbers, their sum and their
average over the union of `Ranges`, computed from the stored cells (each stored cell tested against the
ranges, so overlapping ranges count a cell once) and cached by the ranges and the document's version.
A number is read with `decimal.TryParse` in `NumberStyles.Float` and the page's culture, which the
transpiler already carries (`DecimalConversionConformanceTests`), so `7,01` is a number in `pt-BR` and
text in `en-US`, until slice 5 types values. The `StatusBar` component listens to `Refreshed` as the
bars do and writes `SdkStrings.Sum`, `Average`, `Count` and `AddToSelection` in the page's language.

### 9. The macOS shell offers a key to the host before the input method while nothing is marked

The other shells and the browser already do. While a composition is in flight the input context keeps
the first look, so its candidate window answers its own keys; otherwise the key goes to the host, and a
key the host leaves goes on to the input context as before. Dead keys and input methods still compose,
since the host claims no printable key without ⌘ or ⌃ in a text target, which Windows, iOS and Android
already rely on.

## Risks / Trade-offs

- [A summary over a whole-sheet selection walks every stored cell] → cached per document version and
  ranges, so a keystroke that moves nothing recomputes nothing; benched at 100 000 × 100 with the whole
  sheet selected, and the budget fails rather than warns.
- [The macOS key order changes for every text target] → proved on a real window with the CGEvent
  driver for a dead key and a Japanese composition in a sheet, a code editor and a text field before and
  after, and by the shells' existing tests.
- [Headers without a pressable lose their button semantics until 2b] → accepted and named in the
  pull request; 2b gives them the grid's own roles.
- [A gliding mark on Photon keeps the frame clock running] → only while a glide is in flight, the same
  rule `TransitionClock` follows.

## Migration Plan

Preview break: `SelectRows(from, to)` and `SelectCols(from, to)` become `SelectRows(anchor, focus,
add)` and `SelectCols(anchor, focus, add)`; `controller.SelectRows(r, r)` is
`controller.SelectRows(r, r, add: false)`. No other caller changes: `Selection` keeps its type and
meaning, as the active range.
