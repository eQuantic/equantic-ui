# Tasks

## 1. The cell key at Excel's extent

- [x] 1.1 `CellRef.Key` and `CellRef.FromKey` move to `long`, and `SheetDocument`'s cell dictionary keys
      by `long`; verify the engine builds with no warning and `SheetControllerTests` stays green
- [x] 1.2 Pin the last cell of Excel's grid in .NET (write, read back, `ToA1` is `XFD1048576`, key
      17 179 869 183, two rows 262 144 apart kept apart) and in `sheet-model.spec.ts` against the
      transpiled engine, where a `long` is a BigInt; verify the .NET test fails on the `int` key (it read
      −1) and both pass on the `long` one (the browser never overflowed, which was the disagreement)
- [x] 1.3 Regenerate the engine's transpiled pins (`EQ_UPDATE_TRANSPILED=1`) and verify the diff moves
      only the key's arithmetic

## 2. The vocabulary

- [x] 2.1 Add `TableSpans` to the vocabulary, its uniform and sparse forms to the components
      (`UniformTableSpans`, `SparseTableSpans`: sorted overrides, prefix sums, transpiled like the rest),
      and `TableOffset` (two doubles); verify by xUnit tests of offset, index-at and total over
      1 048 576 rows with overrides at both ends
- [x] 2.2 Add `TableRange`, `TableScrollRequest` (an offset or a range to reveal, with a version) and the
      `ITableScrollModel` door; the `TableViewCell` node (row, column, `Rows`, `Columns`, `Mark`, keyed
      by its position) and the `TableViewport` node (spans, the scroll model, cells, `PinnedRows`,
      `PinnedColumns`, `Request`, `OnScrolled`, `OnViewportChanged`, `Width`, `Height`, `Label`),
      sealed, each with its `Visit`, its `ChildSlots` case and its `LayoutTransparency` entry; verify
      the solution builds once every visitor has its case
- [x] 2.3 Add the `TableViewport` factory to `UI`, and the `PublicAPI.Unshipped.txt` lines; verify
      `UiFactoryConformanceTests` and the public API analyzers pass
- [x] 2.4 Move the reflection guards that count or list nodes (`ClosedHierarchyTests`,
      `LayoutTransparencyTests`, `FlutterParityPinTests`, `MainSizeKindCoverageTests`,
      `FactoryArguments`, `LabelledNodesReachSemanticsTests`, `NeutralVocabularyTests`); verify each
      fails before its entry and passes after
- [x] 2.5 The runtime's twins: `nodes.ts`, `vocabulary.ts`, the generated node kinds
      (`EQ_UPDATE_NODE_KINDS_TS=1`), the exports fixture (`EQ_UPDATE_PRIMITIVES_FIXTURE=1`), `index.ts`
      and `runtime-exports.ts`; verify `tsc` through `-t:TestRuntime` and the generator tests pass
- [x] 2.6 `docs/FLUTTER-PARITY.md`: the `RenderSliver` row says what the two-dimensional viewport
      covers, a `TableView` row is added with its probe; verify `FlutterParityPinTests` passes

- [x] 2.7 Unify `PointerKind`: the Photon host's own copy (mouse, touch) moves into the vocabulary,
      which adds the stylus; verify the solution builds and the hover tests stay green
- [x] 2.8 eqc: an array literal of a vocabulary enum's values is typed by the enum's union (it was a
      `string[]` to TypeScript, and the scroll model's loop over its axes failed the runtime's
      typecheck); verify `EmittedTypeSurfaceTests` fails without the fix and passes with it

- [x] 2.9 The axis is an abstract CLASS (`TableSpans`), not an interface: in a component's constructor
      an interface is a dependency the container provides, and `TableView(rows, columns, …)` resolved
      both axes from it; verify the transpiled `TableView` takes its axes as arguments
- [x] 2.10 eqc: a class extending a hand-written vocabulary twin imports it through the runtime (it was
      imported from `./TableSpans`, a module nobody writes); verify `EmittedTypeSurfaceTests` fails
      without the fix and passes with it

## 3. The scroll model, written once

- [x] 3.1 `TableScroll` in `eQuantic.UI.Components`, implementing `ITableScrollModel`: offset, clamping, the remainder an axis could not take, the wheel (pixels,
      lines, pages, Shift), the glide of a wheel step, the honoured request version, and the reveal of a
      range around the pinned bands; verify by xUnit tests for every scenario of the `table-viewport`
      spec that the model decides alone
- [x] 3.2 The pan and the fling on a supplied clock, with `ScrollClock`'s constants and the
      reduced-motion rule, and the scrollbars' geometry (thumb size and floor, a drag's mapping, a
      track press's page); verify by xUnit tests, including the thumb dragged to the end reaching the
      last row of 1 048 576
- [x] 3.3 Transpile it and run the same scripts in Vitest against the transpiled module; verify the
      offsets agree with .NET's to the last digit, and the transpiled pin is recorded

## 4. Photon's wheel on two axes

- [x] 4.1 macOS reads `scrollingDeltaX` beside `scrollingDeltaY`; Windows handles `WM_MOUSEHWHEEL`
      and turns a Shift+wheel horizontal; verify each shell builds and its input tests pass
- [x] 4.2 The host's wheel carries both deltas to the topmost region that can move on each axis and
      hands the remainder to the region around it; a horizontal `ScrollView` takes the horizontal
      delta, or the vertical one when there is none; verify `PhotonHost` tests for each scenario of the
      `scrolling` spec, and that `CodeEditorComponentTests` stays green
- [ ] 4.3 Prove a horizontal swipe on a Mac trackpad moving a horizontal scroll view in the
      `PhotonDesktop` sample

## 5. The viewport on Photon

- [x] 5.1 Measure: cells tight to their extents at their positions relative to the window, the four
      bands, the unbounded axis taking the visible extent a `ScrollView` now carries down in the
      constraints (or the window's), with its development log; verify layout tests for the pinned
      alignment at three widths and offsets past 2²⁴ px
- [x] 5.2 Emit: the four bands under their clips and input narrowing, the marks per band, the
      scrollbars in their strips, a `TableRegion`; verify goldens for the bands, a mark across the
      pinned row and both scrollbars, light and dark
- [x] 5.3 The host: the wheel, the pan and fling, the scrollbar drag and the track press routed to the
      model the node carries, its clock advanced while it moves; the request's version revealed after the frame; the
      reports once per change; verify `PhotonHost` tests for every scenario of the `table-viewport`
      spec on Photon
- [x] 5.4 Semantics: the viewport is a labelled group over the cells it holds; verify
      `LabelledNodesReachSemanticsTests`

## 6. The viewport on the web

- [x] 6.1 The TypeScript lowering: the clipping box, the four band layers moved by transform from a
      base reset when the cells change, the scrollbars, the non-passive wheel taking only what the
      model took, the pointer events (a touch pans, a mouse press is the cell's), the request's version,
      the reports; verify Vitest specs driving it with real events for each scenario of the spec the
      browser decides
- [x] 6.2 The unbounded axis on the web: an empty box on a filled axis takes the nearest scrolling
      ancestor's visible extent, or the window's, kept by a `ResizeObserver`, with its development
      log; verify a Vitest spec, and the correction at hydration
- [x] 6.3 The C# server lowering writes the same boxes at offset zero and hydration adopts them;
      verify an SSR fixture and the hydration contract tests
- [ ] 6.4 Run the Server suite and record the served runtime's gzip size against its budget; verify the
      budget test passes, or move its line and say why in the pull request

## 7. The `TableView` component

- [x] 7.1 `TableView(rows, columns, cellBuilder)` with `PinnedRows`, `PinnedColumns`, `Overscan`,
      `Marks`, `Request`, the reports, `Width`, `Height` and `Label`; the window with hysteresis; cells
      keyed by row and column; verify component tests on Photon for the cells built and the scroll
      inside the overscan that builds nothing
- [x] 7.2 Its factory in `UI` and its transpiled pin (`EQ_UPDATE_TRANSPILED=1`); verify
      `UiFactoryConformanceTests` and `SharedComponentTranspilationTests`
- [x] 7.3 The wiki: a `TableView` page and its line on the components page, in English, Portuguese,
      Spanish, French and German, on a wiki branch named like this pull request's; verify the wiki
      guards against that branch with `EQ_WIKI_DIR`

## 8. Proved against the real thing

- [x] 8.1 A `/table` screen in `DefaultUIDashboard`: 1 048 576 × 16 384 cells showing their addresses,
      one pinned row and one pinned column; prove in Chromium the last row reached, the headers aligned
      at 400, 900 and 1 440 px, Shift+wheel, a thumb dragged to the end, and the table inside the
      console's scrolling page taking its visible height
- [ ] 8.2 The same screen in `PhotonDesktop`; prove the trackpad on both axes, a fling, the scrollbars
      and the pinned bands
- [x] 8.3 A bench for a scroll step and a window rebuild at 1 048 576 × 16 384 on both targets, with a
      budget that fails; record the numbers in the pull request

## 9. Closing

- [ ] 9.1 `docs/SPREADSHEET-PLAN.md`: slice 0a delivered, D18 closed; a ledger entry under
      `docs/ledger/` citing #982
- [x] 9.2 Every affected suite green on this machine (Compiler, Web, Server, Native.Engine, Conformance,
      the runtime's Vitest), `dotnet build` of the three samples, `./scripts/check-openspec.sh`
- [ ] 9.3 Archive the change with `openspec archive a-viewport-on-two-axes --yes` before the merge
