# Proposal

Closes #1036, a Bug: a box's inset highlight was drawn on the web and never on Photon.

## Why

`BoxStyle.InsetHighlight` documents both targets: on the web an inset box-shadow entry, on Photon a hairline
fill clipped to the top edge of the rounded rectangle. The web realizer writes the shadow. No native project
read the member, so a box that set it was glossy in a browser and flat on Photon, with no diagnostic. Nothing
in the SDK sets it today, so no shipped component showed the difference; an app that set it did.

## What Changes

- Photon draws the highlight as the member says: a hairline one point tall along the top of the padding box,
  over the fill, inside the border, and clipped to the box's corners, which is where CSS paints an inset
  shadow one point down.
- The member stays in the vocabulary: the issue allowed removing it, and the two targets now draw it the same.

For a developer: nothing to write. A box that sets `InsetHighlight` shows it on Photon as it does in a browser.
No public surface moves.

## Capabilities

### New Capabilities

- `box-painting`: what a box paints over its fill, the same on the web and on Photon.

## Impact

- `src/eQuantic.UI.Native.Components/EmitVisitor.Chrome.cs`, Photon's emission of a box's chrome.
- `tests/eQuantic.UI.Native.Engine.Tests/InsetHighlightTests.cs`, the reference backend's pixels beside the
  web realizer's shadow for the same box.
