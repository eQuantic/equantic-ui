## 1. The vocabulary

- [ ] 1.1 `Sortable.Indent`, `SortableItem.Depth` and `SortDrop.Depth`, and `UI.Sortable(…, indent:)` and
  `UI.SortableItem(…, depth:)`; their twins in `vocabulary.ts`, `nodes.ts` and `value-types.ts`.
- [ ] 1.2 `SortPlacement.Wanted`, `SortPlacement.Depths` and `SortPlacement.Siblings`; the twin regenerated.
- [ ] 1.3 Check: `SortPlacement` tests for the range, the candidates' order, the rounding and the siblings, each failing
  without its rule.

## 2. The web

- [ ] 2.1 The C# and TS lowering write a list's indent and an item's depth, and nothing for none.
- [ ] 2.2 `dom/sortable.ts`: in a list with depths the carried item's subtree is no place, a place between items carries
  the depth the pointer's travel asks for, offered to the guard by `Depths`; the keyboard's cross arrows step the depth
  and then move to the neighbouring list; the words are filled by `Siblings`.
- [ ] 2.3 Check: vitest carries an item deeper and shallower by the pointer and the keyboard, falls back past a refused
  depth, never offers a place in the carried subtree, and hears the level; the parity fixture lowers a list with depths
  identically on both sides. Each fails without its change.

## 3. Photon

- [ ] 3.1 The host's pointer and keyboard carry the same depths, by the same arithmetic, and say the same words.
- [ ] 3.2 Check: `SortableHostTests` carry the same list as vitest and read the same drops and words; each fails without
  its change.

## 4. The tree

- [ ] 4.1 `TreeView` gives its rows their depths and its sortable its indentation, leaves the carried node's subtree out
  of the places, lands a node under the ancestor the depth means and draws the line at the depth's indentation.
- [ ] 4.2 Check: `tree-view-move.spec.ts` and `TreeMoveTests` drop a node as the last child of an open folder by the
  pointer and by the keyboard, and a child at the top level after its folder; the line follows the depth.

## 5. Records

- [ ] 5.1 Public API in the branch's files; `docs/FLUTTER-PARITY.md`'s sortable row names the depth; the ledger entry.
- [ ] 5.2 The wiki's Sortable and TreeView sections say how a list's items take depths and how a tree's node lands at
  one, in five languages, on the branch's wiki branch.
- [ ] 5.3 Check: the affected suites, the runtime's tests, a tree in a browser, `check-openspec.sh`.
