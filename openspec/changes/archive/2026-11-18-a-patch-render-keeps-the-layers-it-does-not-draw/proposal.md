# A patch render keeps the layers it does not draw

Closes #1575, a bug under #1174 (the transpiler's fences hold on every path, part four). It blocks the
web half of #1555 (LayoutBuilder).

## Why

On the web, a render a page's owners ask for lowers again only the components that built and patches
the DOM they drew (#1455). It still ended the owner's frame as if it had drawn the whole page, and
`BuildOwner.EndFrame` takes out every deferred layer the frame did not show. So an open `Overlay` or
`Anchored` panel anywhere else on the page had its components unmounted while its DOM stayed: a press
inside it changed nothing. A press inside the open layer itself did the same, since the patch never
shows a layer and the marked component waited for it.

## What changes

- `BuildOwner.DrewOnly(parts)` lets a realizer that draws only parts of the tree say which ones. At
  `EndFrame` a deferred slot outside them keeps its layer, and one inside them that the frame did not show
  is taken out, as a whole frame takes it out. A frame whose realizer says nothing drew the whole tree,
  which is every frame Photon, the server and a whole web render draw: one rule for both kinds of frame.
- `BuildOwner.WaitsForALayer` says a marked position waits for its layer to be shown. The browser's patch
  render asks it, and a render with such a position lowers the page whole, which shows the layer and
  builds it.
- The browser's patch render (`relowerBuilt`) says what it drew: the components it lowered again, or
  nothing when it falls back to a whole render.

For a developer nothing is written differently: an open overlay or panel stays alive while the rest of
the page changes, and one that closes still leaves. The public surface gains `BuildOwner.DrewOnly` and
`BuildOwner.WaitsForALayer` in `eQuantic.UI.Primitives`, both for a host that draws an element tree, not
for an app; the developer surface does not move. The engine's TypeScript twin is generated again.

## Principles

It follows 4 (written once: the rule lives in the engine, transpiled to the browser, and every realizer
keeps it) and 9 (the structural fix: the frame says what it drew, rather than the patch showing again
layers it never lowered).
