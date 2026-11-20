## Context

`Sortable` (#1535) places a carried item by `SortPlacement.Place`: an index among the other items along the axis, and in
a list that nests the item it lands inside. `TreeView` (#1533) is a nesting sortable over its visible rows, and reads an
index as "before this row, among its siblings", or after the last row. The browser's controller (`dom/sortable.ts`) and
the Photon host (`PhotonHost.Sorting`) read boxes and keys and run the same transpiled arithmetic. #1594 asks that a
nesting sortable also say how deep the pointer means at the end of a subtree, and that the tree read it.

## Goals / Non-Goals

**Goals:**

- A place between two items says the depth the carried item takes, by one arithmetic both targets run.
- The pointer picks the depth across the axis, and the keyboard reaches every depth too.
- A reader hears where the item is at that depth.
- The tree lands a node under the ancestor the depth means and draws the line at that depth.

**Non-Goals:**

- Dimming the carried node's subtree. Its rows stay as they are drawn; only the carried row is dimmed.
- Mirroring the cross direction with the layout direction. The pointer's travel and the arrows are physical today, as
  every gesture is; mirroring them is #1569's.
- Collapsing a carried folder's children while it is carried, as some trees do.

## Decisions

**The depth is data on the nodes, not read from the boxes.** `SortableItem.Depth` says each item's depth and
`Sortable.Indent` the distance across the axis between two depths. A realizer could guess depths from where each item's
content starts, but a row builder, a padding or an icon moves that start, and Photon and the browser would guess apart.
The tree knows its levels and its indentation, so it states them. A list with `Indent` 0, the default, has no depths
and behaves as before.

**The pointer's depth is relative to where it picked the item up.** It asks for the item's own depth plus one per
`Indent` travelled across, rounded to the nearest (`SortPlacement.Wanted`), as dnd-kit's sortable tree projects it.
Reading the pointer's absolute position instead would need to know where depth 0 starts across the list (its padding,
the row's inset, its direction); the travel needs none of it, and a pointer that does not travel across keeps the item
at its own depth, clamped to what the place allows.

**The place allows from the depth below to one deeper than the item above.** Coming before the item after the place,
the carried item is at least that deep; it may be a later sibling of the item before the place or of any of its
ancestors down to that depth, or the item's child, one deeper. Without the one deeper, an item that was the only child
of its folder could not be put back where it was. The candidates (`SortPlacement.Depths`) are the wanted depth kept in
that range, then each shallower one, then each deeper one, and the realizer offers them to the list's guard in that
order: a pointer next to a file, where the child depth is refused, lands at the file's own level rather than nowhere.
The guard is asked once per place and wanted depth, as it is asked once per place now.

**A carried item carries its subtree.** In a list with depths, the items right after the carried one that are deeper
than it are its descendants: they are no places of their own, on either target, and the tree leaves the same rows out
of the places it reads. Before this, a place among them was offered to the guard and refused (a node inside itself);
now the indices the sortable reports and the rows the tree reads stay aligned once the subtree is left out.

**The keyboard's cross arrows step the depth first.** In a list with depths, at a place between items, ArrowRight asks
one deeper and ArrowLeft one shallower (along a horizontal list, ArrowDown and ArrowUp), passing over refused depths as
the arrows along the axis pass over refused places; once the place offers no depth that way, the arrow moves to the
neighbouring list of the group, as it did. The depth the keyboard last chose is kept as the wanted depth while the item
moves along, as the pointer's travel is.

**The words keep their holes.** A step's `{1}` position, `{2}` count and `{3}` list stay, filled from the depths in a list
with depths: the position and the count among the items at the item's depth under the same holder, the holder being the
nearest earlier item one shallower, and `{3}` that holder's name, or the list's at the top (`SortPlacement.Siblings`). A
list without depths is a list whose items are all at depth 0, so its words do not change. No new step was needed, and
an app's own words keep working.

**The tree computes its move from the depth alone.** The parent is the ancestor, at one shallower than the depth, of
the row before the place; the sibling it lands after is the last visible child of that parent before the place
(`SiblingBefore`, as now). With no depth (a theme whose tree indentation is 0) the tree reads the place as before.

## Risks / Trade-offs

- [A pointer that drifts across while carrying along the axis changes the depth] → the depth moves only a whole
  `Indent` (16 px by default) from where the item was picked up, rounded, and only where the place allows more than one.
- [The flat position said before, "position 5 of 9 in Files", becomes the position among siblings] → a tree's reader
  gains the level, and a list without depths says exactly what it said.
- [The guard is asked up to one more time per depth the place allows] → a tree's depth is a few levels, and each answer
  is kept for the place.
