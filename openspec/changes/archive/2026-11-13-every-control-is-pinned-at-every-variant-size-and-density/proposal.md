# Proposal

Closes #1247, the last slice of #294, after T1 (#1245) and T2 (#1246).

## Why

The production-ready definition asks that every shipped component pass variant matrix tests, and
nothing drew a control at more than the variant and size its own tests chose. A button lowered at
`Link` and `XLarge`, or a badge at `Info` under the compact density, was never looked at, so a theme
slot, a lowering or a layout change could alter it with every suite green.

## What Changes

- `ComponentTester.Css` gives a test the atomic stylesheet the frame is written against. An atomic
  class is a hash of its rule, so `Html` alone said that a look changed and never what it became.
- A shared catalog lists the shipped components from the build output, with how each is built. The
  semantics parity fixture (#339) reads it, and so does the matrix.
- The variant matrix: every component of the catalog is drawn at every `Variant` and `SizeVariant` it
  accepts, through its widest constructor or a property an author sets, and at both densities. Each
  cell is pinned three ways: the markup and the rules the web lowers, what a screen reader hears, and
  every box Photon lays out. The pins are files under `tests/…/VariantMatrix/`, one per control, and
  are rewritten by `EQ_UPDATE_VARIANT_MATRIX=1`. A new control joins with no edit to the test.
- A cell that cannot exist is a line of `Coverage/variant-matrix.baseline.txt` with its reason: a
  control with no variant, a control with no size, a value a control refuses (`TextInput` at `Small`),
  or a target it cannot be drawn on. The baseline may only shrink.

Nothing an app writes changes, except that a test can now read `ComponentTester.Css`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `component-testing`: a test reads the stylesheet a frame is written against, and the SDK's own
  controls are pinned at every variant, size and density they accept.

## Impact

`src/eQuantic.UI.Testing/ComponentTester.cs` and its public API, and, in
`tests/eQuantic.UI.Native.Engine.Tests`, `ComponentCatalog`, `SemanticsParityTests`,
`VariantMatrixTests`, the pins and the baseline. Also `ROADMAP.md`, the ledger, and the wiki's
Testing page.
