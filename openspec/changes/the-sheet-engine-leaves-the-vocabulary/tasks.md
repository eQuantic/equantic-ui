# Tasks

## 1. The engine's assembly

- [ ] 1.1 Create `src/eQuantic.UI.Sheet` (csproj referencing `Primitives` only, `PublicAPI` files) and
      add it to `eQuantic.UI.sln`
- [ ] 1.2 Move `Primitives/Sheet/*` into `Document/` and `Editing/`, one type per file, namespace
      `eQuantic.UI.Sheet`, without `[TwinIsTranspiled]`; drop the four entries of
      `one-type-per-file.baseline.txt`
- [ ] 1.3 Reference it from `Components`, `Web.Build`, both SDKs' `Sdk.props` (package and source
      tree) and the test projects that name its types
- [ ] 1.4 List `eQuantic.UI.Sheet` in `RuntimeProvidedTypeScanner` (namespaces and assemblies) and in
      `EnumUnionsTsGenerator`
- [ ] 1.5 Check: the solution builds with zero warnings, and `AssemblyLayeringTests` holds the new
      row

## 2. The protocol

- [ ] 2.1 Add `ISheetSurfaceModel` to `Primitives`; `SheetSurface` takes it as `Model` and loses the
      four window properties; the `SheetSurface` factory follows
- [ ] 2.2 `SheetGrid` and `SheetController.Grid`; `CellAt`, the fill handle's grab and the press
      gesture move into the controller, which implements the interface
- [ ] 2.3 `SheetKeymap.Handle` takes the modifiers and the clipboard; `CopyTsv`/`PasteTsv` become
      `CopyText`/`Paste`, and `Cut` arrives
- [ ] 2.4 `Spreadsheet` hands the engine its grid in `Build`
- [ ] 2.5 Check: engine tests for every scenario of the spec (press, drag, Shift, double click, fill
      handle, the clipboard with and without a clipboard), executed against the controller

## 3. The hosts

- [ ] 3.1 `PhotonHost` forwards key, text, pointer and clipboard to the model, keeping only the press
      flag and Escape-leaves
- [ ] 3.2 The web: `vocabulary.ts` and `nodes.ts` carry `model`, `lowerSheetSurface` forwards
      `keydown`, the press with its click count, the moves and release, and copy/cut/paste
- [ ] 3.3 Regenerate the transpiled twins (`EQ_UPDATE_TRANSPILED=1`) and every fixture the move
      touches, reading each diff
- [ ] 3.4 Check: the sheet suites on both targets (`SheetSurfaceTests`, `SheetEditingTests`,
      `SpreadsheetGoldenTests`, the runtime's `sheet-*.spec.ts`), the Web and Conformance suites, and
      the dashboard's `/sheet` page driven in a browser: click, drag, double click, fill, ⌘C/⌘V
- [ ] 3.5 The first press in the browser is read before the focus moves anything, and the focus
      takes no scroll: found driving the dashboard in 3.4, pinned by a runtime spec proved both ways

## 4. The documentation

- [ ] 4.1 CLAUDE.md's project structure, the audit's section 4 and its order of attack, ROADMAP,
      docs/FLUTTER-PARITY.md's `TextEditingController` row and its pin, docs/HANDOFF-FIDELITY-AUDIT.md
      citations
- [ ] 4.2 The wiki pages that name the engine's types or the package list, in all five languages,
      on a wiki branch named like this one
- [ ] 4.3 A ledger entry under `docs/ledger/` citing #185
- [ ] 4.4 Check: `DocsIndexTests`, the wiki guards against the wiki branch, `check-openspec.sh`
