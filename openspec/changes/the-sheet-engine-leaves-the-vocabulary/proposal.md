## Why

Closes #185, a User Story under Epic #898 (1.0: the web SDK stable, with the native track
experimental).

The spreadsheet's engine (`SheetController`, its document, its editing, its keymap and its model:
1,062 lines and 11 public types) lives in `eQuantic.UI.Primitives`, the assembly whose stated
contents are the abstract visual vocabulary, because `SheetSurface` takes a concrete
`SheetController` and both realizers drive it directly: `PhotonHost` and the web's
`lowerSheetSurface` call its methods, and each host turns a pointer into a cell with its own copy of
the same prefix sums. The code editor's engine left the vocabulary the same way in #359, and the
audit decided on 2026-10-14 that the spreadsheet takes that road before 1.0, because afterwards the
move breaks every app that writes `new SheetController(…)`.

## What Changes

- **BREAKING** The engine moves to an assembly of its own, `eQuantic.UI.Sheet` (namespace
  `eQuantic.UI.Sheet`), between `Primitives` and `Components`, one type per file. The SDKs add it to
  an app as they add `eQuantic.UI.Code`, so no project file changes. An app that names
  `SheetController`, `CellRef`, `SheetRange` or another engine type adds `using eQuantic.UI.Sheet;`.
- **BREAKING** `SheetSurface` depends on `ISheetSurfaceModel`, a new vocabulary interface that says
  everything a realizer may tell the engine (a key, typed text, a pointer, the clipboard's events)
  and nothing else. `SheetSurface.Controller` becomes `SheetSurface.Model`, and the `SheetSurface`
  factory takes the model.
- **BREAKING** The window the surface draws moves from the node to the engine: `SheetSurface`'s
  `FirstRow`, `FirstCol`, `HeaderWidth` and `HeaderHeight` go, replaced by `SheetController.Grid`, a
  `SheetGrid` (the first cell's origin in the surface, and which row and column it is) that the
  component hands the engine when it builds, as `CodeEditor` hands `CodeEditorController.Grid`. The
  factory's `firstRow` and `firstCol` arguments go with them.
- **BREAKING** `SheetController.CopyTsv()` becomes `CopyText()` and `PasteTsv(string)` becomes
  `Paste(string)`, the protocol's names; `Cut()` is new. The text is the same TSV.
- What a press means moves into the engine: the cell under it, a Shift press stretching the
  selection, a double click beginning an edit, a press on the fill handle beginning a fill, a drag
  growing the selection or the fill, and the release landing the fill as one undo step. Neither host
  computes a cell any more, and both answer a pointer the same way by construction.
- The clipboard keys follow the code surface's rule: a host that hands a clipboard (Photon) gets
  ⌘C, ⌘X and ⌘V claimed and served through it, and one that hands none (the browser) gets them left
  for its own copy, cut and paste events. On Photon a cut with no clipboard no longer clears the
  selection it could not copy.
- `[TwinIsTranspiled]` leaves the moved types, since their namespace now says what the attribute
  said; the forms' models keep it.
- A press in the browser is read where it landed. The web half focused the sheet before reading the
  point, and focusing a sheet taller than its view scrolled the scroller around it first: measured on
  the dashboard, 127px on the first press, which selected the cell five rows below the one pressed.
  The defect predates this change and was found driving the dashboard to prove it; the point is read
  first now, and the focus takes no scroll, as the code surface's does.

Migration, one line: add `using eQuantic.UI.Sheet;` where the app names the engine's types, call
`CopyText`/`Paste` for `CopyTsv`/`PasteTsv`, and set `SheetController.Grid` where a hand-built
`SheetSurface` set `FirstRow`/`FirstCol`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `spreadsheet`: adds what the engine owns and how a host reaches it: a pointer on a sheet means
  the same on every target, the clipboard keys follow the host's clipboard, and the engine is an
  assembly of its own that the SDK adds.
- `transpiler-classes`: a transpiled vocabulary type is an engine's type, whose namespace says so,
  or one of the vocabulary's own marked `[TwinIsTranspiled]`; the spreadsheet's types are the first
  kind now, and the requirement's scenarios hold unchanged.

## Impact

- New project `src/eQuantic.UI.Sheet` (contract surface, packed like `eQuantic.UI.Code`), its
  `PublicAPI` files, and its place in `eQuantic.UI.sln`.
- `eQuantic.UI.Primitives`: the `Sheet/` folder leaves, `ISheetSurfaceModel` arrives,
  `SheetSurface` changes shape. Its `PublicAPI.Unshipped.txt` carries the removals.
- `eQuantic.UI.Components`: `Spreadsheet` hands the engine its grid; the `SheetSurface` factory
  takes the model.
- The web realizer's runtime half (`lowering.ts`, `vocabulary.ts`, `nodes.ts`) and the transpiled
  twins under `src/shared/components/`; the C# web realizer's SSR is unchanged.
- Photon: `PhotonHost` hands the platform's events to the model and keeps only which surface a
  press began in.
- eqc: `RuntimeProvidedTypeScanner` lists the new namespace and assembly; `eQuantic.UI.Web.Build`
  writes its enums' unions.
- Both SDKs' `Sdk.props` add the package (and the project reference beside a source tree).
- The developer surface (`developer-surface.baseline.txt`) does not move. The samples
  (`DefaultUIDashboard`, `PhotonDesktop`) add the `using`.
