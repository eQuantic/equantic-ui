# Proposal

Closes #691 and #692, both Bugs under #166 (Geometry and semantics live in the vocabulary), found
while fixing #680 in the same pull request. The wiki's #694, a Bug under #313 (Email, Track M: the
real-client matrix and HTML pins), rides along.

## Why

Both are the two-sides question #680 asked of a `Flexible`'s weight, asked again of the rest of the
flex numbers:

- **#691**: `Spacer` clamped its weight with `Math.Max(1, flex)`, and its browser twin with
  `Math.max`, so `Spacer(flex: 0)` or a negative weight took a share of 1 without a word. A spacer
  with no share has no size, and Flutter asserts `flex > 0` for its own `Spacer`.
- **#692**: the server wrote a `Flexible`'s basis through `TokenCss.Px`, two decimals at most, and
  the twin wrote it raw. Measured: a basis of 540.125 was `540.13px` on the server and `540.125px` in
  the browser, and the float 540.12, which the transpiled code holds as 540.1199951171875, was written
  with all of its digits. Each side minted its own atomic class for one style, so the browser did
  not recognise the class the server rendered.

## What Changes

- **A flexible Spacer's weight is 1 or more**, and a weight below 1 is refused where it is written:
  an `ArgumentOutOfRangeException` from the C# init accessor, so the constructor, the factory and an
  object initializer all meet it, and a `RangeError` from the twin once its trailing config is
  applied. The rigid form, `Spacer.Fixed`, keeps the one zero a spacer holds.
- **A basis is written by the rule every other length uses.** The twin writes it through `px`, the
  twin of `TokenCss.Px`, so both sides write `540.13px` for 540.125 and `540.12px` for the float
  540.12.

For a developer: `Spacer(0)` and `Spacer(-1)` fail where they are built, as `Flexible(child, flex:
-1)` does since #680; nothing that wrote a weight of 1 or more moves. A page whose Flexible declares
a fractional basis hydrates without repainting it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `flex-layout`: a new requirement, a flexible Spacer's weight is 1 or more, and the requirement that
  the server and the browser write the same declaration now names the rule the basis is written by.

## Impact

- Primitives: `Nodes/Spacer.cs`, whose weight is checked on its accessor.
- The runtime: the `Spacer` twin in `vocabulary.ts`, and `lowerFlexible` in `lowering.ts`.
- Not reached: the C# web realizer (it already wrote the basis through `TokenCss.Px`), Photon (it
  reads the weights the nodes now refuse to hold wrong), eqc, the shells, the SDKs and the templates.
  Every Spacer in the tree already has a weight of 1 or more.
- The public surface does not move: `Spacer`'s constructor and its `Flex` property keep their
  signatures. The developer surface does not move. The break is in behaviour: a Spacer weight below 1
  throws. The migration line: write `Spacer()` for a share of 1, or `Gap(dp)` where a rigid gap was
  meant.
- The wiki, in English and Portuguese: the EmailRealizer page's example builds the bulletproof button
  as a Link around a Box, and its Track M sentence says a Button is refused (#694); WriteOnceComponents
  says a Spacer's weight is 1 or more (#691).
