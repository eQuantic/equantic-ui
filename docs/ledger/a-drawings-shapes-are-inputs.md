- **2026-11-03 · A drawing's shapes are inputs**: [#674](https://github.com/eQuantic/equantic-ui/issues/674), under
  Feature #194. A `VectorShape` with `OnPressed` is pressed where its path is painted, by its fill rule, reached by
  Tab, pressed by Enter and Space, announced as a button by its `Label` with `Selected` as its state, and painted
  in its `Hover` (fill, stroke, opacity) under the pointer; the keyboard's focus strokes its outline. On the web
  each one is a `path` with a button's role and `pointer-events: visibleFill`, the same tree from the server and
  the client; on Photon a hit region tests the path by its winding number under a part id of the drawing's
  position, so the press, the focus, the semantics and an activation name the same shape. The dashboard sample
  shows it at `/map`. Proposed and archived through OpenSpec (`drawing-inputs`).
