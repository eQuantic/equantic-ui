# Proposal

Closes #1460 and #1461, Bugs under #1174.

## Why

B8's frames draw the unselected filter chips ("All", "Expenses") outlined: no fill, a 1dp
`BorderStrong` edge and `TextPrimary` text. The only chip that rests on `SurfaceSubtle` is the
removable Input chip ("rio-trip.pdf"). `Chip.cs` rested the unselected filter chip on the Input
chip's `SurfaceSubtle`, so its `SurfaceSubtle` press showed nothing, and its hover had to step to a
midpoint toward `Border` that no block names.

The Input chip's label is `TextPrimary` in the frame, where the chip drew it `TextSecondary`.

The selected chip's hover stays as it is, #1462: B8's "fill→pressed midpoint" between an opaque fill
and a translucent press, averaged in straight alpha, lands past the press rather than between the
two, so reading it needs a decision about the midpoint itself, which the Tonal `IconButton` shares.

## What Changes

- The unselected filter chip rests outlined and hovers and presses on `SurfaceSubtle`, as an outline
  `Button` does.
- The Input chip's label is `TextPrimary`, and its close glyph `TextMuted`, the frame's `--tx3`.

## Impact

- `src/eQuantic.UI.Components/Chip.cs` and its transpiled twin, the Wave 1 goldens, the parity
  fixture and the README's dashboard picture.
- Every unselected filter chip of an app looks different at rest. No API changes.
