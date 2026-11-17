## 1. The shared arithmetic and the strings

- [x] 1.1 `SortPlacement.KeyStep(index, into, step, others, nests)`: the places before, inside and after, kept inside
  the list; `Step` when the list does not nest. Its twin is written again (`EQ_UPDATE_TRANSPILED=1`).
- [x] 1.2 `SortMovedInto` and `SortDroppedInto` in the SDK's four cultures, the designer, `SdkStrings` and the
  runtime's generated strings.
- [x] 1.3 Check: `KeyStep` tested on its own in C# and through the twin in vitest, at both ends and in the middle.

## 2. The web's keyboard path

- [x] 2.1 `dom/sortable.ts` starts a keyboard carry from a focused composite's active item (`aria-activedescendant`
  inside it), and after a drop leaves the focus on the composite.
- [x] 2.2 In a nesting list the arrows step through `KeyStep`'s places, report `Into`, and say "inside" with the
  item's name, for the step and for the drop.
- [x] 2.3 Check: vitest drives a nesting list by keyboard into the item above, and a composite whose active item is
  picked up keeps the focus; each fails without its fix.

## 3. Photon's keyboard path

- [x] 3.1 `PhotonHost.Sorting` starts a keyboard carry from the active cell's item (`ActiveItemOf`) and steps a
  nesting list through `KeyStep`'s places.
- [x] 3.2 Check: `SortableHostTests` carry an item into the one above by keyboard, and a tree's active node is picked
  up from the tree's stop; each fails without its fix.

## 4. The tree

- [x] 4.1 `TreeMove`; `TreeView.OnMoved` and `CanMove` in `AdoptConfig` and the factory's tail; `ListTheme.TreeDropColor`
  in its `Merge`; the public API declared in the branch's file.
- [x] 4.2 With `OnMoved`, the `Navigable` inside a nesting `Sortable` and each node row a `SortableItem` (disabled when
  busy or while a field is open); with none, the tree drawn exactly as before.
- [x] 4.3 The drop read into a `TreeMove`, a no-op dropped, the cycle guard and `CanMove` in `Accepts`, the moved node
  busy while the task runs, then its old and new parents read again.
- [x] 4.4 The indicator: the line on the row's top edge (the last row's bottom edge) at the row's indentation, the
  ring and tint on the row it lands inside, in a `Stack` that sizes nothing.
- [x] 4.5 The pause: a closed node with children, hovered inside for 700 ms, opens; another place or the end forgets it.
- [x] 4.6 The catalog census lists `TreeMove` if it carries text (it does not), the variant pins and the component
  pins are written again, and the parity fixture holds a tree that moves.
- [x] 4.7 Check: vitest drives the tree by pointer (before, after, inside, the cycle, `CanMove`, the pause, the busy
  task and both reloads) and by keyboard; Photon's tests do the same through the host; each fails without its fix.

## 5. Sample, documentation, proof

- [x] 5.1 The dashboard's `/files` moves files and folders by pointer and by keyboard, laid over the server's listing.
- [x] 5.2 The wiki's Components page, in its five languages, on a branch named like this one; the ledger entry
  `docs/ledger/a-tree-moves-its-nodes-by-drag.md` citing #1533; FLUTTER-PARITY's sortable row names the tree.
- [x] 5.3 Check: `/files` in the browser, a file moved into a folder by pointer and one by keyboard, a folder refused
  inside itself; the Web, Server, Native and Compiler suites and vitest green.
