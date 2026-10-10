# Proposal

Closes #669, #670 and #671, sub-issues of #166 (geometry and semantics live in the vocabulary), under
#158 (Primitives carries only the vocabulary).

## Why

An `AdaptiveNode` holds up to three arms: the web mounts every arm behind a gate whose media rules
show it in its range, and Photon lays out the arm the window resolves. Building eQuantic Auth's
landing page found three ways an arm was not laid out as written:

- A gate's name kept a fractional threshold's dot (`eq-vc703.7037`), which a selector reads as two
  classes, so the browser dropped every rule of the gate and each arm showed at every width (#669).
- The gates are `display: contents`, so an arm is laid out by the node's parent, yet every arm was
  lowered on no axis: a `Spacer` or a `Gap` arm lowered to nothing (#670).
- A `Stack` asks a child whether it is positioned, and an `AdaptiveNode` is not: a `Positioned` arm
  degraded to its child and joined the flow (#671).

Photon had the last two, measured, for the same reason: a container asked the node, not its arm.

## What Changes

- **A gate spells a threshold's point as an underscore.** `eq-vc703_7037`, read back by both twins;
  the media condition, which is a length, keeps the dot. The threshold travels as ten-thousandths of
  a dp rounded from the single's exact value, the integer both producers compute, so they spell it
  alike: C#'s `"0.####"` of a float rounds to seven significant digits first, and had spelled
  1066.6667 `1066.667` on the server where the browser wrote `1066.6667`. Every whole threshold keeps
  its name.
- **An arm is laid out by the parent the node stands in.** On the web a container places each arm by
  its own rule for a direct child, inside the arm's gate: a flex's axis and align-self, a grid's span,
  a stack's anchor or cell. A parent with no such rule lowers the arms on the axis it gave the node.
  On Photon a line and a grid lay out the resolved arm in the node's place, and a stack reads
  `Positioned` off the node the child measured to, which is the arm.
- **The node's own `AlignSelf` and `GridSpan` are not read**, on either target: the node is never laid
  out itself. Photon read them until now, and the web never applied them.

For a developer using the SDK: on the web a breakpoint may be any number, and on both targets
`AdaptiveNode(Gap(24), null, Gap(64))` is a spacing that follows the window, a `Positioned` arm is
anchored in its `Stack` at the widths its arm serves, and an arm's own `AlignSelf` and `GridSpan`
place it.

Migration: an app that set `AlignSelf` or `GridSpan` on an `AdaptiveNode` says it on each arm.
Nothing in this repository sets them.

## Capabilities

### New Capabilities

- `adaptive-layout`: how an AdaptiveNode's arms are gated on the web, and laid out by its parent on
  every target.

### Modified Capabilities

None.

## Impact

- The web realizer: `StyleAtomizer.cs` (`AdaptiveGates`), `WebLoweringVisitor.cs` (`Decorate`, the
  AdaptiveNode door) and `WebLoweringVisitor.Containers.cs` (`Place`, `LowerLayer`, `LowerFlexItem`,
  `LowerGridItem`, `LowerAdaptive`).
- The runtime: `style-atomizer.ts` (the gate names and conditions) and `lowering.ts` (`placeChild`,
  `lowerLayer`, `lowerFlexItem`, `lowerGridItem`, `lowerAdaptive`), the twins of the above.
- Photon: `LaidOutChildren` (new), `LayoutContext.ArmOf`, the flex, wrapped-flex and grid passes, and
  `PositionedOf`. On the way, the wrapping line measures a child again at the path it measured it at
  first: its count skips a Spacer, and a pane that grew behind one was measured again at the
  Spacer's path. The path is kept beside the item when the line first measures it, never read back
  off the node it measured to: an AdaptiveNode measures to its arm, stamped one level under the
  node's own path, so an arm the line grew or shrank was measured again a level further down.
- Primitives: `AdaptiveNode`'s XML docs say where an arm's placement is read.
- Not reached: eqc, the transpiled twins, the shells, the SDKs and the templates.
- The public surface and the developer surface do not move.
