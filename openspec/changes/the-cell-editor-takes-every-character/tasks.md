# Tasks

## 1. The code engine measures

- [x] 1.1 `CodeGrid` gains `Func<string, float>? Measure` (last positional parameter, defaulting to
      null), and `CodeEditorController`'s geometry (`CaretRect`, `SelectionBandsIn`, `PositionAt`,
      `CharacterAt`, the hover's and the press's reads) goes through two private helpers, the x of a
      column on a line and the column at an x; with a measurer, the x is the measured prefix at a text
      element boundary, kept per line text as `CellsOf` keeps cells, and the column is the nearer
      boundary found by a binary search; verify new `CodeMeasuredGridTests` for the spec's caret, press
      and band scenarios with a stub measurer
- [x] 1.2 The column a vertical move aims at becomes a distance from the line's start (`_desiredCell`
      → a desired x), the same number a monospaced grid computed; verify the spec's vertical-move
      scenario measured, and that `CodeEditorController`'s existing ↑/↓ tests and `CodeCellsTests` pass
      untouched
- [ ] 1.3 Prove the monospaced editor did not move: the transpiled pins regenerated
      (`EQ_UPDATE_TRANSPILED=1`) differ only in the code engine's module, the code editor's goldens and
      `CodeKeymapBehaviourTests` pass unchanged, and a twin test (`CodeMeasuredGridTwinTests`) runs the
      measured scenarios through the runtime and matches .NET
- [ ] 1.4 `docs/CODE-EDITOR-PLAN.md`'s proportional fence says what is true now: the engine places a
      measured face when its grid carries a measurer (the spreadsheet's editors, #983), and the code
      editor stays monospaced; verify `DocsIndexTests` (repoint any citation the change moved)

## 2. The vocabulary's text door for a sheet

- [ ] 2.1 `ISheetSurfaceModel` takes the code surface's door: `HandleKey` gains the
      `KeyboardConvention`, and `SetComposition`, `Carets`, `InputBounds`, `FocusVersion` and
      `FocusChanged` join, with doc comments that say who calls each and when; `SheetSurface.CaretColor`;
      `SheetTheme.EntrySelection`; their TypeScript twins (`nodes.ts`, the theme twin); declare the
      `PublicAPI` lines; verify the vocabulary builds with no warning, the interface-defaults and
      primitives fixtures regenerate (`EQ_UPDATE_INTERFACE_DEFAULTS=1`, `EQ_UPDATE_PRIMITIVES_FIXTURE=1`)
      with only these lines moving, and `EnumValueAbiTests` passes

## 3. The entry in the sheet's engine

- [ ] 3.1 `eQuantic.UI.Sheet` references `eQuantic.UI.Code` (the csproj comment says why); the runtime
      bundles the sheet's modules after the code engine's (`RuntimeProvidedTypeScanner`'s order and the
      barrel), with no static initializer building another module's class at evaluation; verify the
      Server's `BundleRuntime`, the runtime's Vitest and `SharedComponentTranspilationTests` after
      regenerating the pins
- [x] 3.2 `SheetController.Entry` (a plain-text `CodeEditorController` with `KeyBindings` giving back
      indent, outdent, line comments, line moves, copies, deletes and inserts, and the completion,
      hover, signature and problem keys), `SheetEditMode`, `EditMode`, `ToggleEditMode()`,
      `BeginEdit(seed, mode)` (history cleared, caret at the seed's end), `CommitEdit` writing
      `Entry.Document.Text` as one `SheetEdit`, `CancelEdit`; `Draft`, `TypeIntoDraft` and
      `EraseFromDraft` removed and every caller moved; verify `SheetControllerTests` for the spec's
      "entry is a text document" scenarios and the moved `SheetEditingTests`
- [x] 3.3 `HandleText` and `SetComposition` at rest begin an Enter-mode entry that replaces the value,
      and while editing go to the entry; `FocusChanged(false)` cancels a composition and keeps the entry;
      `Carets` and `InputBounds` answer from the active view; verify `SheetSurfaceModelTests` for the
      composition, cancellation and emoji scenarios (`👍🏽` typed, one Backspace empties it)
- [x] 3.4 The clipboard while editing: `CopyText`, `Cut` and `Paste` act on the entry's selection and
      caret (a paste is one step of the entry's undo), at rest on the cells as before; verify the spec's
      copy-while-editing and paste-while-editing scenarios through `HandleKey` with a clipboard and
      through the three methods
- [ ] 3.5 A twin test drives the same sequence of keys, text and compositions through the transpiled
      engine and through .NET and compares the entry, the caret and the cells after each step; verify it
      runs in the runtime's Vitest and `SheetTwinTests` (or the suite that hosts the engine's twins)

## 4. The keymap of an entry

- [ ] 4.1 `SheetKeymap.Handle` takes the convention; while editing, the sheet's keys first (Enter, Tab,
      Escape, F2, the Enter-mode arrows, Alt+Enter's line break, Command+Enter through a new
      `FillSelection`), the rest through `Entry.HandleKey`, returning before the sheet's undo; verify a
      table-driven `SheetKeymapTests` per key in both modes and both conventions, including undo while
      editing never reaching the sheet's history
- [x] 4.2 At rest: Backspace begins an empty Enter-mode entry over the active cell only, Delete clears
      the selection, F2 begins Edit mode with the value; verify the spec's Backspace and Delete scenarios
- [ ] 4.3 The date and time chords by convention (Ctrl+; and Ctrl+Shift+;, ⌃; and ⌘; under Apple's,
      `;` or `:` as the key), at rest and while editing, writing `SheetController.Now()` with the
      culture's `"d"` and `"t"`; verify the spec's `en-US` and `pt-BR` scenario in .NET and through the
      runtime with the page's culture set, both pinned to the same text
- [x] 4.4 Escape releases Tab (`TabReleased`): set by Escape at rest, cleared by any non-modifier key
      before anything else is decided, Tab and Shift+Tab unclaimed while set, Escape while editing
      cancelling and keeping the hold; verify the engine-level scenarios of the requirement

## 5. A press while editing

- [x] 5.1 `EditorRange`: the active cell widened in whole columns while the widest line (measured, plus
      padding) needs room and in whole rows while the lines do, capped at the last column and row in
      view; the cell view's grid origin slides so the caret stays inside past the view's edge; verify
      the spec's three growth scenarios against the engine with a stub measurer and a viewport
- [x] 5.2 `HandlePointer` while editing: inside the editor's bounds the press, its moves and its release
      go to the entry (Edit mode, the cell view active), outside it commits and acts at rest; a double
      click at rest begins Edit mode with the caret at the press; `CursorAt` answers the text beam over
      the editor; verify the spec's press-inside, press-outside, double-click and pointer scenarios

## 6. The cell editor drawn

- [ ] 6.1 `EntryLines`, a component of the family that draws a code document's lines in a given type,
      with the active view's selection bands under them and the composition underlined; verify a
      component test on both targets (a band under `world`, a composition underlined) and the parity
      fixture's new case (`EQ_UPDATE_PARITY_FIXTURE=1`) written the same by C# and the runtime
- [ ] 6.2 `Spreadsheet` hands the cell view's geometry (origin, line height, the measurer over the
      cell's type) on every build, adds the editor mark over `EditorRange` (background, outline,
      `EntryLines`) and drops the ring and the handle while editing; `SheetSurface.CaretColor` from the
      palette; a cell's `Text` shows its lines from the top; verify Photon goldens light and dark of an
      entry wider than its cell, of two lines and of a composition, each with an assertion beside it
      (`EQ_UPDATE_GOLDENS=1`), and `SpreadsheetComponentTests` for the multi-line cell
- [ ] 6.3 The 0b bench with an entry open: a keystroke at 100 000 × 100 on Photon and in the runtime
      under its ceiling; verify `SpreadsheetPerfTests` and `spreadsheet-perf.spec.ts`

## 7. The web's door

- [x] 7.1 Move the door out of `lowerCodeSurface` into one function both lowerings call (the textarea,
      `keydown` for claimed commands, `beforeinput`, the composition kept per model, the clipboard's
      events, focus and blur, the soft keyboard's input types); verify every existing code surface spec
      (`code-surface*.spec.ts`, the IME and clipboard specs) passes unchanged
- [x] 7.2 `lowerSheetSurface` takes the door: the textarea at `InputBounds` carries the `tabindex` and
      the accessible name, the `div` keeps `role="grid"`, the carets are painted and blink as a code
      surface's, focus requests are honoured per model, the `keydown` fallback that typed `event.key`
      goes, and the browser's reload keys are never handed to the model; the C# twin writes the same
      textarea; verify the parity fixture (`EQ_UPDATE_PARITY_FIXTURE=1`), `RoleBearingHostBoundsTests`,
      and the hydration spec over the server's markup
- [ ] 7.3 Web specs with the events a browser sends: a dead key's composition (`´` then `a` committed
      as `á`), a Japanese composition shown then committed, an emoji's `insertText`, an AltGr character,
      a copy and a paste while editing, F5 left to the browser; verify them in
      `spreadsheet-text-door-web.spec.ts`

## 8. Photon's door

- [ ] 8.1 Measure first, before changing anything: drive the macOS shell's text input client
      (`setMarkedText:`, `insertText:`) on a sheet in a real window and record what reaches the model
      and what is drawn; write the measurement into the pull request
- [x] 8.2 `SetMarkedText` hands a sheet target its composition; the emit paints and blinks the sheet's
      carets (the code surface's caret emit generalised over both); `CaretInFrame` answers a sheet
      region's `InputBounds`; focus requests are adopted for sheets; verify host tests for the
      composition (shown, committed, cancelled), the emoji, the candidate window's anchor at the caret and
      at the active cell, and the blink restarting on a key
- [ ] 8.3 Prove it in a real Metal window through the shell's own input client: a dead-key sequence, a
      Japanese composition and an emoji land in a cell, drawn; capture the window

## 9. The formula bar

- [x] 9.1 `SheetController.FormulaBar`, an `ICodeSurfaceModel` over the entry with the bar's geometry:
      a press or text begins an Edit-mode entry and makes the bar the active view, Enter, Tab and Escape
      act as in the cell and ask for the sheet's keyboard, `Carets` only while the bar is active; the
      controller assigns `Entry.Grid` from the active view; verify engine tests for the spec's three
      scenarios, the active view switching both ways
- [ ] 9.2 `FormulaBar(controller)` in `eQuantic.UI.Spreadsheets` with its factory in `SpreadsheetsUI`,
      its accessible name in `SdkStrings` in every language the resources carry; verify on Photon and
      in Chromium that the bar and the cell show the same text after each of `a`, `b`, `c`, that
      editing from the bar commits and returns the keyboard, and `UiFactoryConformanceTests`

## 10. The name box and Go To

- [ ] 10.1 `SheetReference.TryParse` (a cell, a range, whole rows, whole columns, `$` anchors, either
      case, out of the grid refused) and `SheetController.GoTo` (select, reveal the first cell, ask for
      the sheet's keyboard); verify table-driven tests including `xfd1048576`, `B:D`, `3:5`, `$B$7`,
      `B0`, `XFE1` and `A1048577`, in .NET and through the twin
- [ ] 10.2 `SheetNameBox` (its own one-line entry, the address at rest, `nR x mC` while a drag of more
      than one cell is in flight from an `SdkStrings` format, its text selected on focus, Enter going
      to, `Invalid` on a name that names nothing, Escape restoring and returning the keyboard) and
      `NameBox(controller)` with its factory, counting itself on mount and unmount; the Go To chord
      and, on Photon, F5 give it the keyboard when one is mounted; verify the spec's scenarios on both
      targets, F5 unclaimed in Chromium, and the chord unclaimed with no name box

## 11. The keyboard leaves a sheet

- [ ] 11.1 Escape then Tab, and Escape then Shift+Tab, in a page and at either end of a dialog that
      does not close on Escape, on Photon (host tests over a real frame's focus stops) and in Chromium
      (a web spec with the modal layer's trap); verify the requirement's four scenarios and that #732's
      report no longer reproduces

## 12. Samples and documentation

- [ ] 12.1 The dashboard's `/sheet` and the PhotonDesktop gallery's Spreadsheet section place the name
      box and the formula bar over the grid, as Excel lays them out; verify `dotnet build` of the four
      samples, and take again any README picture `ReadmeCapturesTests` names, looking at it before
      recording (`EQ_UPDATE_README_CAPTURES=1`)
- [ ] 12.2 `docs/SPREADSHEET-PLAN.md`: slice 1 delivered in "Where the slices stand", D5, D12 and D15
      closed, slice 2 next; the wiki's Components page (the cell editor, the formula bar, the name box,
      the keys) and the Upgrading page (the migration lines) in English, Portuguese, Spanish, French and
      German, on the wiki branch named like this pull request's; verify the wiki guards with
      `EQ_WIKI_DIR` pointed at it, and `WikiClaimsCompile` for every snippet the pages add
- [ ] 12.3 One ledger entry for this event citing #983 and #732, as a file under `docs/ledger/`;
      verify `LedgerTests`

## 13. Integration

- [ ] 13.1 In Chromium on the dashboard's `/sheet` (this worktree's server): `á`, `ã`, `ç` by dead keys,
      a composed Japanese word and an emoji land in a cell; F2 then the arrows, Home and End move the
      caret; the formula bar follows keystroke by keystroke; Escape then Tab leaves the sheet; the
      served runtime's budget moves in its commit with the number (`EQ_UPDATE_RUNTIME_BUDGET=1`)
- [ ] 13.2 The suites after a merge of `main`: Compiler, Conformance, Web (against the wiki branch),
      Server, Native.Engine, Design, Email, the runtime's Vitest, `./scripts/check-openspec.sh`, and the
      four samples built; `/code-review high` on the branch and its findings fixed before the pull
      request opens; the change archived (`openspec archive the-cell-editor-takes-every-character --yes`)
      before the merge
