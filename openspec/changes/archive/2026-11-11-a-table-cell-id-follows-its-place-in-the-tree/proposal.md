# Proposal

Closes #1115, a Bug under #849, and #1426, a Bug under #1174.

## Why

On the web, a table or a grid gives each cell an id that its row's `aria-owns` names, and a `Navigable` grid gives each cell one its `aria-activedescendant` names. Both scoped those ids by the grid's label, hashed, because the hash was the one thing the server's first pass and the browser's lowering computed alike.

A label is not unique on a page. Every `Spreadsheet` is labelled alike, an unlabelled table hashed the empty string, and a `Calendar` is labelled by its month. So two spreadsheets on one page, two unlabelled tables, or two calendars of one month wrote the same ids. #1331 then made a group table view's reading order own its cells too, scoped the same way, and a group is a table view's default role, so two unlabelled table views on one page were the likely case (#1426). `getElementById` then resolved the second grid's references to the first grid's cells, and a reader in the second grid heard the first grid's values.

## What Changes

- The server's lowering follows the tree the way the browser's already did: each container names the step to each child by a segment (`LoweringPath`), the same one on both sides, so each node has a path that is the same on the server's first pass and after hydration, with nothing counted.
- A table's and a `Navigable` grid's cells are scoped by the grid's path below its root, hashed with the atomizer's FNV (`LoweringPath.Scope` and its TypeScript twin `pathScope`). Two grids on one page hold different places in the tree, so their cells' ids differ whatever they are labelled.
- The scope reads past the step into what a component builds, which both producers spell `~`. The browser mounts a page, and a component a page builds as its whole tree, as roots of their own, with what they build at the root; the server lowers the page as a node of the tree and steps into what it builds. Measured on the dashboard's `/sheet`, where the two scopes differed by that one step until the scope read past it. Inside a tree the browser still takes the step, because its reconciler names each component by its path.
- An app writes nothing new: two spreadsheets, two unlabelled tables or two calendars of one month on one page each name their own cells. No public surface and no developer surface moves: `LoweringPath` is internal.

## Impact

The web realizer (`WebLoweringVisitor` and the new `LoweringPath`) and the browser runtime's lowering (`lowering.ts`). The cells' ids change spelling, from the label's hash to the path's, on both producers alike, so hydration adopts them as before. The segment rules are written once on each side, and the component parity fixture is what holds them equal; a TypeScript twin generated from `LoweringPath` is a follow-up (#1410).
