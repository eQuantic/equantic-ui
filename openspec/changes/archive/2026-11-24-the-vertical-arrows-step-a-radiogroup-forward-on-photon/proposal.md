# Proposal

Closes #1873, a Bug under #1587.

## Why

Photon's `Adjustable` mapped ArrowUp to +1 for every role. The web maps the vertical arrows by role: for a tab list and
a radio group, ArrowDown is the next item, as reading order goes. So on Photon a vertical `RadioGroup` or `Tabs` strip
stepped backwards on the vertical arrows, the reverse of the web. Found surveying the arrow keys for #1569.

## What Changes

- **One table of arrow steps, `AdjustableKeys`**, in `eQuantic.UI.Primitives` beside `NavigableKeys` and for the same
  reason: the web realizer and `PhotonHost.KeyDown` read the same key names. Left and right step back and forward for
  every role; a slider's or a separator's vertical axis is value (up increases); a tab list's or a radio group's is
  reading order (down is next); any other key answers null and reaches the page.
- **Both targets read it.** The web realizer's `LowerAdjustable` and Photon's host answer an adjustable's arrows from
  it, and its twin is the runtime's `lowerAdjustable`, which already mapped them so.
- **Right to left** keeps the rule the web had: the horizontal arrows step back and forward, which is WAI-ARIA's
  reading (the APG's tabs, radio and slider patterns say nothing of right to left). Photon lays nothing out right to
  left yet.

The public surface gains `AdjustableKeys` and its `Step`. No member goes, and the developer surface does not move.

## Impact

- `eQuantic.UI.Primitives`: `AdjustableKeys`.
- `eQuantic.UI.Web`: the adjustable's key handler.
- `eQuantic.UI.Native.Components`: the host's adjustable arm.
