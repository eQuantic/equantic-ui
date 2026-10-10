# Design

## How Flutter solves it

A Flutter `Flex` hands each flexible child tight constraints for its slot, and a child cannot leave
them, so an item and its child are one size there. The web keeps them apart: the item is the flex
box, its child keeps a width of its own and may overflow it, and a scroller's width is capped by its
`max-width: 100%`. Photon follows the web, as everywhere in this pull request. No row of
`docs/FLUTTER-PARITY.md` applies.

## Decisions

**The ceiling is a parent's word that only the bottom node can act on.** That is the shape
`LayoutConstraints.Truncating` already has: the slot decides, the scroller acts, and the word travels
through layout-transparent wrappers and nowhere else. `WidthIsACeiling` takes the same road, and the
scroller caps its width while it measures, so its viewport, its clip and its scroll range are all
taken from the capped width. Capping bounds after the measure, which round two did, could reach
neither the node under a wrapper nor the range.

**One item shape for both passes.** `FlexItem` builds the item around its measured child in the slot
and now in the wrapping pass: the item at the size its line gave it, the child keeping a declared size
or pinned to the item. The wrapping pass records each item's resolved size, its hypothetical one
until its line moves it, so a line that holds still still gives a Flexible its basis. A cross stretch
reaches through the item to the child, which is what it reached before the child had an item around
it.

**The column is scoped, not built.** Photon's single-line pass takes an overflow back only in a row,
for every item, and the vertical path is work of its own with its own measurements. This change states
the zero-weight contract for rows and names the gap wherever the contract is stated.

## Risks

- A Flexible on a wrapping line is now a node of its own in the layout tree, as it already was in a
  single line. The native layout classes, the Studio walk and the goldens pass unchanged.
