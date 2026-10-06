# Proposal

Closes #430, a sub-issue of #194 (pointer, gestures and focus), under #190 (Flutter parity: the gaps
that are work).

## Why

Edgar decided on 2026-09-26, while confirming #354, that the pointer exception keeps a floor. Under a
finger every target is at least `Touch.MinTarget` (48dp). Under a pointer a target is its visual
bounds, which is right for a toolbar of 26dp buttons, but `Sizing.SelectionBox(Density.Compact)` is
20dp and sits outside the control ladder: a Checkbox without a label (a table's row selection) or a
Radio's circle is a 20×20 target under a mouse, under the 24×24 WCAG 2.2 SC 2.5.8 asks of a target.
Photon and the web agreed on 20, so the cross-pin passed.

## What Changes

- **A target under a pointer is at least 24dp per side.** `Touch.MinPointerTarget` (24) is the floor
  beside `Touch.MinTarget`. Photon's Compact hit rect grows to it, centred and symmetric like the
  finger's rule, `Sizing.HitTarget(size, Density.Compact)` never answers less, and the web grows a
  fine pointer's target to it as it grows a coarse one's to 48px. The ladder's Compact targets (26,
  32, 40 and 48dp) are past it and grow nothing.
- **The web's slop lies under the control's own content.** It was a pseudo-element over the content,
  so the slop took every hit inside the control as well as around it. Under a coarse pointer that
  went unseen, since a finger does not hover. Under a fine pointer, measured in a browser on the
  dashboard sample, it took the hover from every button: the centre of the Search button hit the
  button element itself, its box never matched `:hover`, and its hover fill never showed. A Pressable
  around an IconButton (the header's Notifications and Help) took every hit the inner control should
  have had. The slop is now the pseudo-element that comes first in tree order, and the content is
  positioned like it, so the content paints and is hit after it, and the slop answers only where the
  control draws nothing.

For a developer using the SDK: nothing to write. A label-less Checkbox or Radio is a 24×24 target
under a mouse, on Photon and on the web. A button keeps its hover fill under a mouse. On a touch
screen a control inside a Pressable takes its own tap, as it already did under a mouse, and the press
still reaches the Pressable around it.

## Capabilities

### New Capabilities

- `hit-targets`: how large a control's target is under a finger and under a pointer, on every target,
  and that the control's own content keeps its hits inside the slop.

### Modified Capabilities

None.

## Impact

- Primitives: `Theme/Tokens.cs` (`Touch.MinPointerTarget`, `Sizing.HitTarget`).
- Photon: `EmitVisitor.Interaction.cs` (`ExpandHitRect`).
- The web realizer: `TokenCss.cs` (the hit slop of each pointer).
- The runtime: `design-system.generated.ts` gains the constant, regenerated from the C#.
- The design handoff: `docs/design/tokens.json` publishes `touch.minPointerTarget`, and Foundations §08
  and `proposals.json` say the floor shipped.
- Not reached: eqc, the shells, the SDKs and the templates.
- The public surface gains one constant, `Touch.MinPointerTarget`. The developer surface does not
  move. Nothing breaks.
