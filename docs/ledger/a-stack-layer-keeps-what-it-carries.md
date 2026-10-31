- **2026-10-30 · A stack's layer keeps what it carries on the web**: two Bugs under #166
  ([#698](https://github.com/eQuantic/equantic-ui/issues/698), [#940](https://github.com/eQuantic/equantic-ui/issues/940)).
  Every cell of a `Stack` was written `min-width: 0; min-height: 0`, which keeps a scroller's content from swelling
  the stack and also took a fixed layer out of the stack's own minimum, so a row squeezed a hugging stack to 0 while
  its content overflowed at the same edge (the spreadsheet's row-number strip beside its grid, 44 on Photon). A cell
  is capped now only where Photon caps the layer: on an axis it fills, on one a scroller hugs, and on both when it
  holds a scroller further down. And the anchor a
  stack builds for a `Positioned` never passed through the funnel, so its key, its bookmark and its origin were
  dropped by both producers and a link to it led nowhere; the anchor carries them. Measured on the web realizer's
  own HTML in Chrome 152, 0 before and 44 after, with the scrollers still capped at 482, and on the dashboard's code
  editor at 700 wide. Proposed and archived through OpenSpec (`layout`).
