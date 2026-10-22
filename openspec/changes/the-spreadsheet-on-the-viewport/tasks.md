# Tasks

## 1. The family's assembly

- [x] 1.1 Create `src/eQuantic.UI.Spreadsheets` (a `contract` assembly above `eQuantic.UI.Components`
      and `eQuantic.UI.Sheet`, packed with its sources under `tools/source` as the charts' are, with
      `PublicAPI` files) and move `Spreadsheet` into it unchanged, with `SpreadsheetsUI` holding its
      `Spreadsheet(controller, …)` factory; add it to the solution; verify the solution builds with no
      warning and the analyzers report the moved lines as `*REMOVED*` in Components and added here
- [x] 1.2 Wire it as the charts are: both SDKs' `PackageReference`, the source-tree
      `ProjectReference`, the static `Using` of `SpreadsheetsUI`, `Sdk.targets` handing eqc its
      sources, `RuntimeProvidedTypeScanner`'s list, the test projects' references, and the packaging
      check's list (`scripts/check-package-surface.cs`); verify `UiFactoryConformanceTests`,
      `DeveloperSurfaceContractTests` (unchanged baseline) and the package surface check pass
- [x] 1.3 Regenerate the transpiled pins (`EQ_UPDATE_TRANSPILED=1`) so `Spreadsheet` ships in
      `runtime.js` from its new home, and move the samples (`SheetScreen`, the gallery's Spreadsheet
      section, `TableScreen`'s `ColumnName`) to the new namespace or the factory; verify
      `SharedComponentTranspilationTests`, the runtime's Vitest and `dotnet build` of the four samples
- [x] 1.4 The wiki's Package Architecture page and the Components page's Spreadsheet section name the
      new assembly, in English, Portuguese, Spanish, French and German, on the wiki branch named like
      this pull request's; verify the wiki guards against it with `EQ_WIKI_DIR`
- [x] 1.5 Prove the real consumer path: the dashboard's `/sheet` serves and takes a click and a key in
      Chromium, and the gallery's Spreadsheet section draws on Photon, both from the moved assembly

## 2. The vocabulary and the viewport

- [x] 2.1 `TableViewport.CanRequestFocus` (default true) and its pass-through on `TableView`: Photon
      registers no stop and routes no key for a viewport that declines, the web writes no `tabindex`
      and takes no key; verify a Photon test and a web spec with a button in a cell (Tab lands on the
      button, never on the viewport) and the parity fixture's table case both ways
- [ ] 2.2 `ITableScrollModel.AutoScroll(Point? pointer)` in `TableScroll`: a velocity per axis from the
      distance past the edges of the part that scrolls, applied in `Advance`, null stopping it, the
      same under reduced motion; verify `TableScrollTests` and its twin in `table-scroll.spec.ts` for
      below, above the pinned row, further-is-faster, the end of the grid and the stop
- [ ] 2.3 Both hosts keep ticking a table that auto-scrolls (Photon's `AdvanceTables`, the web's
      `animate`); verify a Photon host test and a web spec where frames run with a pointer held below
- [ ] 2.4 `SheetTheme` (every member an optional `ColorToken`) and `IAppTheme.Sheet`, a default member
      answering null, with its TypeScript twin; the theme bridge writes `sheet` when the slot is not
      null, as it writes `data`, and the browser's theme answers it; verify the bridge's pinned JSON
      both ways (a theme with a slot, one without) and a spec reading the slot back in the browser
- [ ] 2.5 `ISheetSurfaceModel.HandlePointer` takes the pointer's kind, and every caller passes it
      (Photon's host, the web's lowering, the tests); verify the solution and the runtime's `tsc` build

## 3. The engine on the viewport

- [ ] 3.1 The document answers its rows and columns as `TableSpans` (prefix sums over its size
      dictionaries, rebuilt when a size changes); verify `SheetControllerTests` for offsets and
      index-at with resized rows at both ends of a 1 048 576-row sheet, and the twin in
      `sheet-model.spec.ts`
- [ ] 3.2 `SheetGrid` becomes the viewport's geometry (the scroll model, read live, and the pinned
      header extents); `CellAt` and the fill handle's corner read through the spans and the offset;
      verify the spec's "A press in a scrolled window" in `SheetControllerTests` and its twin, and that a
      point over the header row reads the first visible row
- [ ] 3.3 A reveal version bumped by every command that moves the active cell from a key, a paste, a
      fill, an undo and a redo, but not by a press; PageUp and PageDown by the rows shown under the
      header; verify `SheetControllerTests` for each and the twin
- [ ] 3.4 A finger's press selects on its release and nothing on a cancel; a mouse's and a stylus's
      as before; verify both kinds through the engine in `SheetControllerTests` and the twin
- [ ] 3.5 Auto-scroll from the engine: a selecting or filling drag hands its position to the scroll
      model and null on release or cancel, and `Follow` re-reads the drag's last position when the grid
      moves under it; verify `SheetControllerTests` with a `TableScroll` advanced under a held point

## 4. The spreadsheet on the viewport

- [ ] 4.1 `Spreadsheet` composes a `SheetSurface` over a `TableView`: table row 0 the column headers,
      column 0 the row numbers, both pinned, cells built from their values alone, the headers'
      `Pressable`s and resize grips kept, the table declining the focus; verify Photon component tests
      for the cells built at 100 000 × 100 and the headers aligned with their columns at 400, 900 and
      1 440 points wide and scrolled on both axes
- [ ] 4.2 The marks: the selection's band, the active ring, the fill preview and the fill handle from
      the engine's rectangles; verify the Photon goldens (light and dark, a range across the pinned
      header, a fill preview) reviewed and an assertion of state beside each, and that a selection
      change leaves the cells' nodes unchanged
- [ ] 4.3 The row-number column measured from its widest visible number; verify a Photon test that it
      widens past row 9 999 and the cells still start at its edge
- [ ] 4.4 The reveal request from the engine's version and `Follow` wired from `OnScrolled`; verify
      Photon host tests for forty ↓, PageDown, ⌘↓ ⌘→ at Excel's extent, an undo out of view, and a
      drag held below the grid that keeps extending
- [ ] 4.5 The colours from the sheet slot with the foundation defaults; verify a Photon test with a
      theme that names a gridline colour and one that names nothing
- [ ] 4.6 A finger on Photon: a tap selects on its release, a pan scrolls and selects nothing (the host
      tells the model `Cancel` when the table's pan takes the press); verify Photon host tests
- [ ] 4.7 The wiki's Components page, Spreadsheet section, describes the grid on two axes, the kept
      active cell, the held drag, the marks and the theme slot, in five languages; verify the wiki
      guards and a `WikiClaimsCompile` claim for its snippet

## 5. The web

- [ ] 5.1 The sheet surface takes pointer events with capture instead of mouse events and document
      listeners, passing the pointer's kind; the table's after-pass marks a `pointerup` that ended a
      pan, and the surface reads it as a cancel; verify web specs with real events: a mouse drag
      selects, a finger's tap selects on release, a finger's pan scrolls and selects nothing
- [ ] 5.2 The C# server lowering writes the spreadsheet's boxes and hydration adopts them; verify a
      `spreadsheet` case in the component parity fixture and a hydration spec over its markup
- [ ] 5.3 Run the Server suite and record the served runtime's gzip size against its budget; verify the
      budget test passes, or move its line and say in the commit what the bytes bought
- [ ] 5.4 Prove in Chromium on the dashboard's `/sheet`: the headers aligned at 400, 900 and 1 440 px,
      both axes by the wheel, forty ↓ and PageDown keeping the active cell in view, a drag held past
      the bottom scrolling and extending, the sheet scrolling inside its frame in the console's page

## 6. Photon and the bench

- [ ] 6.1 The gallery's Spreadsheet section on the new component, a sheet of 100 000 × 100; prove it in
      a real Metal window (`--Photon:MaxFrames`) and by its headless picture; retake the README's
      gallery pictures if they change, look at them, and record them
- [ ] 6.2 A bench for a keystroke and a scroll step at 100 000 × 100 on both targets (Photon host, the
      runtime under happy-dom), each with a ceiling that fails and a count of what was built; record
      the numbers in the pull request

## 7. Closing

- [ ] 7.1 `docs/SPREADSHEET-PLAN.md`: slice 0b delivered, D1 to D4 and D6's scroll closed; a ledger
      entry under `docs/ledger/` citing #995
- [ ] 7.2 Every affected suite green on this machine (Compiler, Web, Server, Native.Engine,
      Conformance, the runtime's Vitest), `dotnet build` of the four samples, `./scripts/check-openspec.sh`
- [ ] 7.3 Archive the change with `openspec archive the-spreadsheet-on-the-viewport --yes` before the
      merge
