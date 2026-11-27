- **2026-11-27 · A component lays out its children with a layout written in C#**: a layout the vocabulary did not
  have (a masonry grid, a radial menu) had no door, and `docs/FLUTTER-PARITY.md` listed `CustomMultiChildLayout` and
  `CustomSingleChildLayout` as gaps (#1556, a block of the catalog's 1.0 under #191). `CustomMultiChildLayout`
  takes its children as `LayoutId`s and a `MultiChildLayoutDelegate` the app writes in C#, with Flutter's steps:
  `GetSize`, then `PerformLayout`, where `LayoutChild(id, constraints)` measures a child as the content of a box with
  those bounds and answers its size and `PositionChild(id, offset)` places it; `HasChild` and `ShouldRelayout`, and
  each child laid out exactly once, refused otherwise. A delegate may override `Layout` to size the node from its
  children, which Flutter's cannot. `CustomSingleChildLayout` takes a `SingleChildLayoutDelegate` and is laid out as
  a layout of one. The node takes its room as a `LayoutBuilder`'s box. Photon runs the delegate in its layout pass;
  the browser runs the transpiled delegate after its own layout, with the children the server wrote hidden until it
  places them, and again on every resize. Email refuses both nodes. The dashboard's layout page shows a masonry grid
  and a radial menu ([#1556](https://github.com/eQuantic/equantic-ui/issues/1556)). Proposed and archived through
  OpenSpec (`openspec/specs/layout`).
