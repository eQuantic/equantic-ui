# Proposal

Part of #680, a Bug under #166 (Geometry and semantics live in the vocabulary): it answers Copilot's
second round on the pull request, a defect in the classifier the first round's fix leaned on
(review 5478945716).

## Why

The Slot keeps a child's own main size when `MainSizeKind` reads one, and `MainSizeKind` asked
`CrossSizeKind`, whose list of node types reads a declared size for nine of the fourteen that declare
one. Measured with the web realizer's own HTML in Chrome 154, and on Photon at 8b326209b, in a share of
300:

- a 320-wide `CameraPreview` is 320 in Chrome, in a share and in a zero weight shrunk to 300; Photon
  drew it at 300;
- a `Stack`, a `Canvas` and a `WebFrame` of 400 and of 100 keep 400 and 100 in Chrome; Photon drew
  all of them at 300;
- a `ScrollView` of 100 is 100 in Chrome and of 400 is 300, because the web writes it
  `max-width: 100%`; Photon drew both at 300;
- a box as wide as the window less 32 keeps that width in Chrome; Photon drew it at 300.

## What Changes

- `MainSizeKind` reads every node type that declares a main size: a `SizeValue` it carries or a size
  its constructor demands, through transparent wrappers.
- The Slot keeps a declared or window-relative size, and a `ScrollView`'s width only up to the slot.
- `MainSizeKindCoverageTests` takes the vocabulary's node types from the assembly and fails on any
  that declares a size the classifier does not read.
- `CrossSizeKind` keeps its list in this change, and with it the same gap on the cross axis.

For a developer: a camera preview, a stack, a canvas, a scroller or a window-wide box inside a
Flexible keeps the width it was given on Photon, as in a browser. Nothing changes on the web.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `flex-layout`: the requirement that a Flexible's slot sizes the item and its child keeps its own
  size names every node type that declares one, and the ceiling a scroller's width is.

## Impact

- Photon: `Layout/MeasureVisitor.Intrinsics.cs` (`MainSizeKind`, `MainSizeIsACeiling`) and
  `Layout/MeasureVisitor.Flex.cs` (`Slot`).
- Not reached: the web realizer and the runtime, eqc, the shells, the SDKs and the templates. The
  public surface and the developer surface do not move.
- Measured and left as it is: `CrossSizeKind` misses the same five node types, so on Photon a stack,
  a canvas and a camera preview with a fixed height of 20 are stretched to 100 in a row 100 tall that
  stretches its children, where a box keeps its 20; a scroller and a web frame take the same path.
  And a `WebFrame` cannot cross to Photon at all: it measures nothing there, and now keeps that
  nothing instead of being stretched to the slot.
