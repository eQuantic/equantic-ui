# Tasks

## 1. The vocabulary

- [ ] 1.1 `TableRole` (`Group`, `Table`, `Grid`) and `TableViewport.Role`; `TableCellRole` (`Cell`,
      `ColumnHeader`, `RowHeader`, `None`) and `TableViewCell.Role`, `Selected` and `Current`; `TableView`'s
      `Role`, `CellRole`, `Selected` and `Current`, written onto each cell it builds; `SheetSurface.Announcement`;
      the runtime's node twins and generated enums; declare the `PublicAPI` lines; verify a
      `TableViewTests` case that a built cell carries its role, selection and current flag, and the
      primitives fixture
- [ ] 1.2 `SemanticRole` gains `Table`, `Grid`, `Cell`, `ColumnHeader` and `RowHeader`, appended;
      `CellPlace` and `TableExtent`, and `SemanticNode` widened with them as optional trailing members,
      every call site moved and no second constructor; regenerate the enum baseline
      (`EQ_UPDATE_ENUM_VALUES=1`) and verify `EnumValueAbiTests` and a build with zero warnings

## 2. A table and a grid on the web

- [ ] 2.1 The runtime's lowering of a `Table` or `Grid` viewport: `role`, label, `aria-rowcount`,
      `aria-colcount` and, for a grid, `aria-multiselectable`; the zero-size `rowgroup` of `row`s with
      `aria-rowindex` and `aria-owns` in column order; each cell's `id`, role and `aria-colindex`; a `None`
      cell `aria-hidden`; `aria-selected` and `aria-current` in a grid; the bands, layers and scrollbars
      `role="none"`; a `Group` viewport unchanged; verify `table-viewport-web.spec.ts` cases for the spec's
      "A grid of Excel's extent on the web", "A table", "A group by default", "Headers and a corner" and
      "Selected and current"
- [ ] 2.2 The server's twin in `WebLoweringVisitor.Table` writes the same, ids from the label's FNV scope
      as the `Navigable` grid's; regenerate the parity fixture (`EQ_UPDATE_PARITY_FIXTURE=1`), review its
      diff, and verify `ComponentParityFixtureTests` and `component-parity.spec.ts`, and a
      `TableViewportSsrTests` case for the rows and indices
- [ ] 2.3 The runtime's spreadsheet bench (`spreadsheet-perf.spec.ts`) holds its ceiling with the cells'
      new attributes; verify it and record the measurement in the pull request
- [ ] 2.4 Check against the real thing: the dashboard's table page built with `TableRole.Table`, in
      Chromium's accessibility tree (rows owning their cells across the pinned column, indices, counts)

## 3. A table and a grid on Photon

- [ ] 3.1 The semantics walk: a `Table` or `Grid` viewport announces its role and extent; a cell
      announces its role, place, `Selected` and `Current`, named by the text its content holds, without
      descending into that text and still reaching any control inside; a `None` cell and a mark announce
      nothing; verify `LabelledNodesReachSemanticsTests` and new `TableSemanticsTests` for the spec's
      "The same grid on Photon", "Headers and a corner" and "Selected and current"
- [ ] 3.2 `NativeRole` names the five roles on AppKit, Android and UIKit (`AXTable`, `AXCell`,
      `android.widget.GridView`, the header trait); the macOS bridge sets the row and column index ranges,
      the Android bridge `CollectionItemInfo`; verify `NativeRoleTests` (every role named) and an
      `AppKitAccessibilityTests` case reading `AXTable` and an `AXCell` with its index ranges from real
      AppKit elements
- [ ] 3.3 `AnnouncementParityTests` gains a grid case: the web and Photon announce the same roles, names,
      states and order for a small grid with headers, a selection and a current cell
- [ ] 3.4 Check against the real thing: the PhotonDesktop sample's sheet read with the Accessibility
      Inspector, the grid, the headers and the cells in AppKit's words

## 4. A reader's activation

- [ ] 4.1 `PhotonHost.Activate`: a grid cell or a header with no pressable, link or stop gets a primary
      press and release at its centre through the pointer path; verify the spec's "A cell under a surface"
      with a host test, A/B against the code before it
- [ ] 4.2 The web: a reader's activation arrives as a click on the cell; verify in
      `sheet-surface-web.spec.ts` that a click on a cell with no pointer press before it makes it active,
      and make the sheet answer a click if it does not

## 5. The announcer and the shells

- [ ] 5.1 `LiveRegionAnnouncer` keeps a change made inside the quiet window pending and announces the
      text at the window's end unless it is back to what was said; the host asks for frames while one is
      pending; verify `LiveRegionAnnouncementTests` for the spec's "A change inside the quiet window" and
      "Back to what was said", A/B against the code before it
- [ ] 5.2 iOS drains the queue after each frame and posts the announcement, queued when polite and
      interrupting when assertive; verify it builds for the simulator and, in the simulator, that a
      `Banner` appearing in a sample posts it (the shell's log), and name in the pull request whether it was
      heard on a device
- [ ] 5.3 Android marks a live `Group` node live with its urgency and, for each announcement drained,
      reports its content changed; verify it builds and, in the emulator with TalkBack, that a `Banner`
      appearing is heard, or name in the pull request what the emulator could not run
- [ ] 5.4 Check against the real thing: on macOS, a `Banner` and a `Toast` in the PhotonDesktop sample
      heard by VoiceOver (the first run of `PhotonAnnouncements.Post`)

## 6. The spreadsheet read aloud

- [ ] 6.1 `SheetController.Announcement`: the active cell's address and text, or the word for blank, as
      it moves; the active range's address and size as it grows around the same active cell; empty while
      an entry is open; new `SdkStrings` in English, Portuguese and Spanish and the generated TypeScript
      twin (`EQ_UPDATE_SDK_STRINGS_TS=1`); verify `SheetAnnouncementTests` for the spec's "An arrow", "A
      blank cell" and "A growing range in Portuguese", and a twin test that drives the same keys through
      .NET and the runtime and compares the announcements
- [ ] 6.2 `Spreadsheet` builds its table as a `Grid` named after the sheet, headers as `ColumnHeader` and
      `RowHeader`, the corner `None`, the selected ranges' cells `Selected` and the active cell `Current`,
      and sets `SheetSurface.Announcement`; the sheet's host stops claiming the grid role on both lowerings;
      verify `SurfaceSsrTests`, `sheet-surface-web.spec.ts` and `SpreadsheetComponentTests` for the spec's
      "Where a reader finds a cell" and "The selection and the active cell"
- [ ] 6.3 Both realizers publish the announcement: the web as a visually hidden polite, atomic status
      inside the host, Photon as a polite live group and a live mark carrying its text; verify a web spec
      and a Photon test that an arrow changes what is announced, and "Quick arrows on Photon" through the
      host's frames
- [ ] 6.4 Check against the real thing: the dashboard's sheet with VoiceOver in Safari (the grid, a
      cell's headers, an arrow and Shift+arrows announced, a cell activated) and in Chromium's
      accessibility tree; the PhotonDesktop sheet with VoiceOver on macOS (an arrow and Shift+arrows heard)
- [ ] 6.5 Docs: `docs/SPREADSHEET-PLAN.md` (D17 fixed, the 2b row with #1078 next and #1105),
      `docs/FLUTTER-PARITY.md` (the table semantics row), the wiki's TableView and Spreadsheet pages in its
      five languages on a wiki branch named like this pull request's, and a ledger entry under
      `docs/ledger/` citing #1081

## 7. Integration

- [ ] 7.1 The suites the change reaches, one at a time through the lock: the runtime, Web, Server (the
      served runtime's budget), Native.Engine, Compiler and Conformance; the four samples build; OpenSpec
      validates strictly
- [ ] 7.2 Archive the change (`openspec archive the-grid-is-read-aloud --yes`) before the pull request
      merges, and verify `openspec/specs` reads the new requirements
