## 1. The words on the node

- [x] 1.1 `SortAnnouncements` in `Primitives/Nodes/` with its mirror in `value-types.ts`; `SortWords.Fill` in
  `Primitives/Sorting/`, whose twin the runtime embeds (`EQ_UPDATE_TRANSPILED=1`).
- [x] 1.2 `Sortable(child, id, onDropped, announcements = null)` and its `Announcements`; the doc says what a null
  record does, and that Photon carries an item by drag and by keyboard.
- [x] 1.3 `SdkStrings.Sorting` from the eight `SdkResources` keys; `UI.Sortable`'s `announcements`, filled with it
  when left out; `TreeView`'s sortable given it.
- [x] 1.4 Check: `Fill` tested in C# and through the twin in vitest on the same cases (every hole, a missing hole, a
  stray brace); the factory guard passes and the default is the culture's words.

## 2. The web

- [x] 2.1 The C# and TS lowering write `data-eq-sort-say-<step>` for each text of a list with words.
- [x] 2.2 `dom/sortable.ts` says every step, and describes the focused item, from the words of the list the item
  was picked up from, through the twin's `Fill`; a list with none says nothing. The catalog is no longer read.
- [x] 2.3 Check: vitest drives a carry with an app's own words and reads each step from them; a list with none says
  nothing; the parity fixture lowers a list with words identically on both sides. Each fails without its change.

## 3. Photon

- [x] 3.1 `SortVoiceState` on the source list's position; the host sets it at each keyboard step (picked up, moved,
  moved inside, refused, dropped, dropped inside, put back), naming the item by what its subtree says.
- [x] 3.2 `EmitSortable` records the list as a polite live region with those words; the semantics walk makes its node
  live while it has words, as a nameless group when the list has no label.
- [x] 3.3 Check: `SortableHostTests` drive a keyboard carry and read each step from `TakeAnnouncements`, the app's
  words replacing the SDK's, and the list's semantic node is live with its words; each fails without its change.
  The `PhotonDesktop` sample builds.

## 4. Records

- [x] 4.1 Public API in the branch's files; `docs/FLUTTER-PARITY.md`'s sortable row; the ledger entry.
- [x] 4.2 The wiki's sortable section names the words and how an app gives its own, in five languages, on the
  branch's wiki branch.
- [x] 4.3 Check: the affected suites, the runtime's tests, the dashboard sample in a browser, `check-openspec.sh`.
