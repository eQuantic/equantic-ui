# Proposal

Closes #1598, a Bug under #1587.

## Why

A mouse drag of a sortable item made of text left that text selected on the web, from where the press
started to where the pointer was released. Dragging the dashboard's `Method` heading past `Date` moved
the column and left `DATE` selected. A mouse press on text starts the browser's selection, and the
browser extends it on its own mouse moves. The gesture core prevents the default of each `pointermove`
once a drag is armed, and that does not stop a selection. The table's heading drag (#1601) and the
tree's drag (#1595) run on the same sortable lowering and did nothing about it, and neither did the
draggable and the drag-dismissed sheet, which share the core.

## What Changes

- The gesture core (`dom/pointer-gesture.ts`) holds the selection for every gesture on it. Once a
  gesture arms, it clears the selection the press started. Until the gesture ends it keeps the page's
  `user-select` at `none` and cancels `selectstart`, and then it gives the page back its own
  `user-select`.
- A press that never becomes a gesture leaves the selection to the browser, as before.

## Impact

- `src/eQuantic.UI.Runtime/src/dom/pointer-gesture.ts`. No API changes, and an app writes nothing new.
