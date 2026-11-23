# A component that builds itself is contained in a host

Closes #1544, a bug under #1174 (the transpiler's fences hold on every path, part four).

## Why

A component whose `Build` returns the component itself, or a box around itself, killed the server's
process with a stack overflow when a `MarkupHost` drew it, and the server draws every write-once page
through one. The browser, which lowers the same frame since #1501, hung in a loop and froze the tab.
Without a host the same component is contained by its error boundary and costs its own subtree.

The engine was never the problem: the owner builds each position through the boundary at the depth the
tree reads, and contains the chain at the bound. The lowering was. It found a component's position by
the instance alone, and an instance that builds itself holds every position of the chain, so each
lookup answered the outermost one again, whose build is the same instance again.

## What changes

- A host's lowering finds a component inside the component position whose build it is lowering, on the
  server (`ElementFrame`, `WebLoweringVisitor`) and in the browser (`element-frame.ts`, `lowering.ts`).
  It descends the positions the engine built, as Photon's layout already walks them, so the chain ends
  where the engine contained it and draws the boundary's surface, with its siblings and the page around
  it drawn.
- The guard stays in one place, `ComponentBoundary`'s bound: the engine contains the chain there (read
  from the tree's depth), as the lowering with no host contains it (counted by its walk). The lowering
  through a host adds no second bound, so the failure is reported once.
- A stack's child, which a stack reads through to what its component built before it places it, takes
  the same path, and so does a render the browser lowers again from one position (#1455).

For a developer nothing is written differently: a component that never stops building now costs its own
subtree on every target, as one that throws does. No public surface and no developer surface moves:
`ElementFrame` and the browser's frame are internal to their realizers.

## Principles

It follows 9 (the structural fix: the lowering follows the tree instead of a second guard bounding a
wrong walk) and 4 (written once, realized per target: the server and the browser lowerings change in
the same way, and Photon, which already followed positions, is the model).
