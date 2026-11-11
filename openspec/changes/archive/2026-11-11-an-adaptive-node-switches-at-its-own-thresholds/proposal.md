# Proposal

Closes #1465, a Bug on the 1.0 critical path that blocks #1453 (E3b-1), and #735, the same defect seen
from Photon.

## Why

An `AdaptiveNode` declares its own thresholds (`MediumFrom`, `ExpandedFrom`). The web gates each arm
with them, while Photon's layout (`LayoutEngine.ArmOf`) and the element tree (`ChildSlots.Of`) resolved
the arm by the window's size class (`Resolve(SizeClass)`), at the spec's 600 and 840 dp.
`AdaptiveNode.ResolveWidth`, the form that honours a node's thresholds, had no caller. The dashboard's
payments page (`ExpandedFrom = 1024`) drew its grid on Photon between 840 and 1024 dp where the web
stacked it. And once the browser builds through the element tree (#1453), the arm on screen is the one
the web's gate shows while the element holds the one the size class picks: between a node's threshold and
the spec's, the visible arm would be one no element holds, and its stateful components would start again
on every render.

## What Changes

- The element tree's owner is handed the window's width (`BuildOwner.Build(root, context, windowWidth,
  rebuildAll)`) and holds the arm `ResolveWidth` answers for it.
- Photon's layout resolves the arm by the window's width it lays out at, with the node's thresholds, and
  `LayoutContext.SizeClass` goes.
- `AdaptiveNode.Resolve(WindowSizeClass)` goes: nothing could honour a node's thresholds through it.
- The runtime's `AdaptiveNode` twin gets `resolveWidth`, the same rule, for the element tree the browser
  builds next.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `adaptive-layout`: every target resolves an arm by the window's width at the node's own thresholds

## Impact

A break in the contract: `AdaptiveNode.Resolve` and the old `BuildOwner.Build` overload are gone (the
Upgrading page says what to write instead), and `LayoutContext.SizeClass` on the experimental native track.
