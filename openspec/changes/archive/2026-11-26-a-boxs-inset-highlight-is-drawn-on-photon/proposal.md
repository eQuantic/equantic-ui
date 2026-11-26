# Proposal

Closes #1036, a Bug under #1587.

## Why

`BoxStyle.InsetHighlight` documents both targets: an inset box-shadow entry on the web, and a hairline fill clipped to the rrect's top edge on Photon. The web realizer draws it (`inset 0 1px 0 <color>`, through `ShadowList`). No native project read the member, so a box that set it had its highlight in the browser and none on Photon, with no diagnostic. This is the same class of defect as #629, a bordered box the two targets laid out differently.

## What Changes

- **Photon draws the highlight** where CSS paints an inset shadow. It fills one dp row along the top of the box's padding box, clipped to that box's rounded corners. The fill sits over every background layer (solid, grid, glow, gradient) and under the border.
- **The padding box** is the box past the border on each side that has one. Its corners are rounded by the outer radius less the larger of the two borders beside each corner, so the clip never reaches past the inner edge. CSS shrinks each of a corner's two radii by its own border, which gives an ellipse.
- `BoxStyle.InsetHighlight`'s documentation names the padding box.

No public surface and no developer surface moves.

## Impact

- `eQuantic.UI.Native.Components`: `EmitVisitor.Chrome`.
- `eQuantic.UI.Primitives`: `BoxStyle.InsetHighlight`'s documentation.
- A new spec, `box-paint`, and `InsetHighlightParityTests` with its golden.
