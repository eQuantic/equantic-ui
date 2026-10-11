# Design

## How Flutter solves it

Flutter hands a `Flexible`'s child the slot as its constraints, tight or loose, and a `SizedBox`
inside cannot leave them: a 400-wide box in a 300 slot is 300 there. The web does not clamp: the
realizer writes the box `width: 400px; flex-shrink: 0` as a block inside the item, and a browser lets
it overflow. Photon follows the web here, as everywhere in this pull request, because the two
realizers have to agree and the web's CSS is the one a developer already ships.

## Decisions

**The slot sizes the item, and a declared size is kept.** The wrapper takes the slot on the main
axis. A child whose main size is declared, which `CrossSizeKind` already answers on the cross axis
(Fixed for a fixed width, an `Image`, an `Icon`, a `Vector`, a `Drawing`, a `Spinner`, looking through
transparent wrappers), keeps what it measured, in both directions, because Chrome keeps a fixed child
at 400 in a 300 item and at 100 in a 300 one. `MainSizeKind` asks the same statement on the main
axis, so the two cannot drift.

**A child without a declared size still fills the slot.** An auto or Fill box measures to the slot
already, since it is stretched on the main axis. A `Text` sizes itself to its lines, and Photon aligns
a Text's lines inside its node's width (`TextAlignmentExtensions.BlockWidth`), so its node keeps the
slot: kept at its measured width, a centred title would start at the item's edge.

## Risks

- A centred `Text` in a Flexible stays centred in the slot on Photon, where the web's inline span
  starts at the item's edge because text-align does nothing on an inline box. That difference is the
  web's and predates this change.
- The wrapping pass measures a Flexible's child with no wrapper node and pins it to the size it
  resolved, so a fixed child there is still drawn at the size the line gave it. That pass has open
  work of its own (#728), and it stays as it is here.
