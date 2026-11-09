# Proposal

Closes #693, #728, #744 and #747, Bugs under #166 (Geometry and semantics live in the vocabulary):
four places where Photon's flex pass resolved a line differently from the CSS the web realizer writes
for the same tree.

## Why

Measured with the web realizer's own HTML in Chrome 152, and on Photon at af3f714e0:

- A row 1440 wide holding `Flexible(pane, flex: 1, basis: 540)` and `Flexible(pane, flex: 1)`: Chrome
  starts the first from its basis and shares the 900 left over, 990 | 450. Photon shared the whole
  row by weight and never read the basis, 720 | 720 (#693). A weight animated to zero at that basis
  glided toward a share of nothing and snapped to 540 on its last frame, where the browser glides
  `flex-grow` down to the basis it holds.
- A wrapping row 1000 wide holding two `Flexible(box 600 wide, flex: 1)` without a basis: Chrome puts
  both on one line at 500, because CSS starts them from `0%`. Photon broke them at the boxes' 600 and
  gave each a line of 1000 (#728).
- A column 400 tall holding a box fixed at 100 and `Flexible(box, flex: 0, basis: 540)`: Chrome
  shrinks the zero weight to 300. Photon's single-line pass took nothing back from an overflowing
  column, so it stayed 540 and ran 240 past the column's end (#747).
- A row 100 tall that stretches its children, holding a `Stack`, a `Canvas`, a `CameraPreview` or a
  `ScrollView` with a fixed height of 20: Chrome keeps each at 20. Photon stretched each to 100, where
  a `Box` kept 20: the cross axis asked a hand-kept list whether a size was explicit, and it missed
  five of the fourteen node types that declare one (#744).

## What Changes

- The single-line pass starts every item from its flex base size, as CSS does: a weighted Flexible
  from its basis (nothing without one), a zero weight from its basis or its content, a rigid child
  from its own size. The leftover is shared by weight on top of each start, and weights that sum to
  less than one, which only an animation produces, share only that fraction of it, as Flexbox §9.7
  does. The start follows the declared weight and the share the resolved one, as the web writes the
  declared basis at once and transitions only `flex-grow`.
- An overflowing line takes its overflow back from every Flexible by its shrink times its start, as
  far as nothing, in a row and now in a column. In a column the other items keep the height they
  measured, which is their min-content height; the text cut stays a row's.
- The wrapping pass breaks a weighted Flexible without a basis on nothing, and a line counts the gap
  between items that ask for nothing.
- One classifier, `DeclaredSizeKind`, answers both axes: the main axis (whether a Flexible's child
  keeps its size in the slot) and the cross axis (whether a stretch may override the child). The
  cross axis's hand-kept `CrossSizeKind` and the grid's `WidthKind` are gone. It is asked of the node
  a child measured to, which now includes what a component built, as the web lowers a component to
  what it builds.

For a developer: a `Flexible` with a basis and a weight is the same size on Photon as in a browser,
in a single line and a wrapping one, a column that overflows on Photon gives its Flexibles' height
back instead of running past its end, and a fixed-height stack, canvas, camera preview or scroller
keeps its height in a row that stretches. Nothing changes on the web. No public surface moves: the
classifiers are internal to `eQuantic.UI.Native.Framework`.

## Capabilities

### Modified Capabilities

- `flex-layout`: a weighted Flexible starts from its basis; a Flexible gives space back by its
  shrink times its start in a column as in a row; a wrapping line breaks a weighted Flexible
  without a basis on nothing; a line that stretches keeps a declared cross size.

## Impact

- `src/eQuantic.UI.Native.Framework/Layout/MeasureVisitor.Flex.cs`, `.Intrinsics.cs` and
  `.Containers.cs` (Photon only).
- The docs of `Flexible.Basis`, `Flexible.Shrink` and `FlexNode.Wrap`. The wiki states none of the
  four gaps, so no page changes.
- Tests: `FlexLineResolutionTests` (new), `DeclaredSizeKindCoverageTests` (was
  `MainSizeKindCoverageTests`).
