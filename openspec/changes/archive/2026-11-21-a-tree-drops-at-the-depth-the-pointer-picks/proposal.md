## Why

Closes #1594, a task of #1528 (TreeView), under the epic #1510 (the components eQuantic Space needs). A `TreeView` that
moves its nodes (#1533) reads a sortable's place as "before this row". Between the last child of an open folder and the
folder's next, shallower sibling there is one index, so a node dropped there always lands at the next sibling's level:
a node can become the last child of an open folder only at the very end of the tree. `SortPlacement.Place` answers an
index along the axis and nothing across it, so no realizer tells the tree how deep the pointer means. Trees that move
nodes by hand (Notion's sidebar, Confluence's page tree, dnd-kit's sortable tree) read the depth from the pointer's
travel across: between the deepest and the shallowest level the gap allows, the line's indentation follows the pointer.

## What Changes

- **A list's items have depths.** `Sortable.Indent` is the distance across the axis between two depths of its items, a
  tree's indentation per level; above 0 the list's items have depths, `SortableItem.Depth` (0 at the top). An item
  carried in such a list carries the items after it that are deeper (its subtree), so they are no places of their own.
- **The place says its depth.** At a place between two items (not inside one), `SortDrop.Depth` is the depth the carried
  item takes: at least the depth of the item after the place, which it comes before, and at most one deeper than the
  item before it, as that item's child. A pointer asks for its item's own depth plus one per `Indent` it travelled
  across since it picked the item up; the keyboard's cross arrows step the depth while the place offers another, and
  move to the neighbouring list of the group as before once it offers none. Each depth is offered to the list's guard,
  and a refused one gives way to the nearest the place allows, shallower first. One arithmetic, `SortPlacement.Depths`,
  written once and run by both targets.
- **A reader hears the level.** In a list with depths each step names the item's position among the items at its depth
  under the same holder, their count, and the holder's name, or the list's at the top: "README.md is at position 2 of 2
  in docs." The words are the list's `Announcements` as they are; only the holes are filled from the depths.
- **The tree reads it.** `TreeView` gives its rows their depths and its sortable its indentation. A node dropped at a
  depth lands under the ancestor of the row above at the depth one shallower, after its last visible child before the
  place: deeper, it is the last child of the subtree's folder; shallower, it lands among that level's nodes. The line is
  drawn at the chosen depth's indentation.

In C#, nothing new to write for a tree; for a list of one's own:

```csharp
Sortable(outline, id: "outline", onDropped: Move, nests: true, indent: 24)
SortableItem(row, id: "intro", depth: 1)
```

Parts reached: the vocabulary (`Sortable.Indent`, `SortableItem.Depth`, `SortDrop.Depth`, `SortPlacement`'s depth
arithmetic), the components (`TreeView`), the web realizer (C# and TS lowering of a sortable and its items), the runtime's
twins and sortable controller, and the Photon host. The public surface grows; nothing goes. The developer surface does
not move.

Principles followed: 1 (the depth is C#, a number on the node), 4 (written once: the range, the candidates and the
words' holes are the transpiled `SortPlacement`, both realizers reading boxes and keys alone), 6 (Flutter has no tree
that sorts; the depth follows the trees that do), 11 (a capability is a node: `Sortable` gains depths for any list, the
tree only uses them).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `sortable-lists`: a list's items may have depths, and a place between two of them says the depth the carried item
  takes, chosen by the pointer across the axis or by the keyboard's cross arrows, with the words naming the level.
- `tree-view`: a node dropped at a depth lands under the ancestor that depth means, and the line follows the depth.

## Impact

- `src/eQuantic.UI.Primitives`: `Sortable.Indent`, `SortableItem.Depth`, `SortDrop.Depth`, `SortPlacement.Depths`,
  `SortPlacement.Wanted`, `SortPlacement.KeyDepth`, `SortPlacement.Siblings`.
- `src/eQuantic.UI.Components`: `UI.Sortable(…, indent:)`, `UI.SortableItem(…, depth:)`, `TreeView`.
- `src/eQuantic.UI.Web`: the lowering of a sortable and its items.
- `src/eQuantic.UI.Runtime`: `lowering.ts`, `vocabulary.ts`, `nodes.ts`, `value-types.ts`, `dom/sortable.ts`, the
  regenerated twins.
- `src/eQuantic.UI.Native.Components`: `PhotonHost.Sorting`.
- Docs: the ledger, `docs/FLUTTER-PARITY.md`'s sortable row, the wiki's Sortable and TreeView sections in five languages.
