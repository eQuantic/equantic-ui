# Tasks

## 1. The selection holds several ranges

- [x] 1.1 `SheetController.Ranges` (every range, the active one last) beside `Selection` (the active
      range); `Select`, `Move`, `MovePage`, `Step`, `GoTo`, `SelectAll` and a plain press leave one range;
      an internal `AddRange` appends one and makes it active; declare the `PublicAPI` lines; verify new
      `SheetRangesTests` for the spec's "A press with Command adds a cell" and "A drag with Command adds a
      range" through `HandlePointer`
- [x] 1.2 `ClearSelection` (Delete) and `FillSelection` (Ctrl+Enter) walk every range in one history
      step; `CopyText`, `Cut`, `Paste`, `FillDown`, `FillRight` and the fill handle read the active range;
      verify the spec's Delete, copy and Ctrl+Enter scenarios, each undone in one step
- [x] 1.3 Add mode: `AddingRanges`, entered and left by Shift+F8, left by Escape; in it a plain press or
      drag adds a range; verify the spec's "Add mode" scenario
- [x] 1.4 `SheetDocument.Version`, advanced by `SetCell` and every structural edit; verify a
      `SheetDocumentTests` case for each mutation, and that undo and redo advance it too
- [x] 1.5 Regenerate the twins (`EQ_UPDATE_TRANSPILED=1`) and run `SheetEntryTwinTests` and a new
      `SheetRangesTwinTests` that drives the same presses, keys and Delete through .NET and the runtime
      and compares the ranges

## 2. Headers answer through the engine

- [x] 2.1 `SheetController.HeaderAt(point)` from the grid's pinned bands and offset (the corner, a
      column, a row, or none), and the header gestures in `HandlePointer`: select the band, Shift
      stretches from the anchor's band, ⌘/Ctrl adds it, a drag stretches it, a finger selects on lift;
      `SelectRows` and `SelectCols` become `(anchor, focus, add)` and every caller moves; verify the spec's
      header scenarios in `SheetHeaderTests`, scrolled and unscrolled
- [x] 2.2 `Spreadsheet` draws headers as plain boxes (no `Pressable`), the corner too, and keeps the
      resize grips as `Draggable` nodes; verify on Photon (`SpreadsheetComponentTests`: a press on a header
      with Shift and with ⌘ through `PhotonHost`, and a grip drag that resizes and selects nothing) and in
      the runtime (`sheet-surface-web.spec.ts`: the same presses through the lowered surface)
- [ ] 2.3 Prove it in Chromium on the dashboard's `/sheet`: a drag across column headers, Shift+click on a
      row header, ⌘/Ctrl+click on a column header, and a grip drag

## 3. Excel's chords

- [ ] 3.1 `SheetKeymap` answers the design's table: Shift+Space, Ctrl+Space (⌃Space on Apple), ⌘A twice,
      ⌘End and ⌘⇧End, Alt+PageDown and Alt+PageUp, Ctrl+Backspace (⌃Backspace on Apple), Ctrl+. (⌃. on
      Apple), ⌘Y, Shift+F8; verify a table test (`SheetChordTests`) over both conventions for every row
- [ ] 3.2 The current region and the last cell in use (design §4); verify the spec's region, empty-cell and
      Ctrl+End scenarios, and that a dense 1000 × 100 block answers ⌘A in under 5 ms in a bench
- [ ] 3.3 The Tab-Enter column (design §5), at rest and through a commit; verify the spec's two scenarios
- [ ] 3.4 A protocol test per chord that drives the same key through `PhotonHost` and the runtime's
      lowered surface and compares the selection and the active cell (`SheetChordParityTests` and its
      runtime twin spec)

## 4. A key reaches a sheet before the input method on macOS

- [ ] 4.1 The macOS shell offers a key to the host first while nothing is marked, and to the input
      context first while a composition is in flight; verify the shell's existing tests, and a host test
      that Shift+Space selects a row with a sheet holding the caret
- [ ] 4.2 Prove it in a real PhotonDesktop window with the CGEvent driver (`.tools/drafts/slice1-issues/ime`):
      Shift+Space selects the row and types nothing; the dead acute then `a` still types `á` in a cell, in
      the code editor and in a text field; a Japanese Romaji composition still shows and commits in a cell
      (ask Edgar before taking the window's focus, and to enable the input source)

## 5. The range drawn as one rectangle

- [ ] 5.1 The sheet slot gains `RangeOutline`, `HeaderWholeBackground`, `HeaderWholeText`,
      `DimmedRangeFill` and `DimmedOutline`, with foundation defaults in `SheetPalette`, crossing the theme
      bridge (`ThemeBridge`, `sheet-theme.ts`); verify `ThemeBridgeTests.EveryMemberOfTheSheetSlot_Crosses`
      and the spec's theme scenarios
- [ ] 5.2 `Spreadsheet.Marks` draws each range as an outline mark and fill marks around the active cell
      (design §6), the headers' band and whole emphasis from the ranges, and the dimmed colours while the
      model has not the keyboard; verify goldens (`sheet-range`, `sheet-ranges`, `sheet-unfocused`,
      `sheet-whole-column`) each with an assertion on the marks' rectangles beside it
- [ ] 5.3 Prove it in Chromium: a range, two ranges, a whole column, and the grid unfocused by Tab

## 6. The cursor glides

- [ ] 6.1 `TableViewCell.Key` and `TableViewCell.Glide` (`MotionSpec?`) in the vocabulary and the
      runtime's node twin, with doc comments; declare the `PublicAPI` lines; verify the primitives fixture
- [ ] 6.2 The web lowers a keyed gliding mark as one element across renders with a transition on its
      left, top, width and height, off under `prefers-reduced-motion`; verify `table-viewport-web.spec.ts`
      cases for a glide, a new mark and reduced motion, and the C# lowering's parity case
- [ ] 6.3 Photon's table emit interpolates a keyed gliding mark by the frame's time and reports active
      motion while it glides, snapping under reduced motion; verify `TableViewportGlideTests` for the spec's
      three scenarios at 0, 100 and 200 milliseconds
- [ ] 6.4 The active ring is keyed `cursor` with `Motion.Base`; verify a Photon frame mid-glide and the
      spec's two cursor scenarios, and in Chromium that → glides and the reduced-motion emulation jumps

## 7. The status bar

- [ ] 7.1 `SheetController.Summary` (count, numbers, sum, average) over the union of the ranges, cached by
      the ranges and the document's version, numbers read with `NumberStyles.Float` in the page's culture;
      verify the spec's three scenarios in .NET and in a twin test in `pt-BR` and `en-US`
- [ ] 7.2 `SdkStrings.Sum`, `Average`, `Count` and `AddToSelection` in `SdkResources` (en, pt-BR, es) and
      their generated twin; the `StatusBar` component and its `StatusBar(controller)` factory in
      `eQuantic.UI.Spreadsheets`, listening to `Refreshed`; verify `SpreadsheetBarsTests` cases on Photon
      and a parity case
- [ ] 7.3 Bench the summary at 100 000 × 100 with the whole sheet selected, in .NET and the runtime, with a
      budget the test fails on; and a keystroke that moves nothing recomputes nothing
- [ ] 7.4 Prove it in Chromium and in the PhotonDesktop gallery: the Portuguese sums of the spec, two
      overlapping ranges, add mode's notice

## 8. Samples and documentation

- [ ] 8.1 The dashboard's `SheetScreen` and the PhotonDesktop gallery place the status bar under the grid;
      take the README's pictures again where a sample's screen changed and record them
      (`ReadmeCapturesTests`)
- [ ] 8.2 `docs/SPREADSHEET-PLAN.md`: slice 2 split into 2a (#984) and 2b (#1081) in the slices table, 2a
      delivered in "Where the slices stand", D10, D13 and D14 closed; verify `DocsIndexTests`
- [ ] 8.3 The wiki's Components page (the selection, the chords, the status bar) in English, Portuguese,
      Spanish, French and German, on the wiki branch named like this pull request's; verify the wiki
      guards with `EQ_WIKI_DIR` pointed at it and `WikiClaimsCompile` for every snippet the pages add
- [ ] 8.4 One ledger entry citing #984 as a file under `docs/ledger/`; verify `LedgerTests`

## 9. Integration

- [ ] 9.1 The suites after a merge of `main`: Compiler, Conformance, Web (against the wiki branch),
      Server (the served runtime's budget recorded with the bytes it bought), Native.Engine, Design, Email,
      the runtime's Vitest, `./scripts/check-openspec.sh`, and the four samples built
- [ ] 9.2 `/code-review high` on the branch and its findings fixed with a test each before the pull
      request opens; the change archived (`openspec archive the-grid-selects-as-excel-does --yes`)
