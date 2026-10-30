# Proposal

Closes #698 and #940, Bugs under #166 (Geometry and semantics live in the vocabulary): two things a
`Stack`'s web lowering lost from a layer, in both producers.

## Why

Measured with the web realizer's own HTML in Chrome 152:

- A hugging `Stack` holding a box 44 wide, beside a box 768 wide in a row 726 wide, or a hugging column
  of them (the spreadsheet's row-number strip): Chrome squeezes the stack to 0 while its content
  overflows at the same edge. Photon keeps 44. Every layer's cell was written `min-width: 0;
  min-height: 0`, which is what stops a scroller's content from swelling the stack, and which also took
  the layers out of the stack's own minimum (#698).
- `new Positioned(child, top: 0, end: -28) { Bookmark = "request" }` in a `Stack`: the page has no
  `id="request"`, so a link to it leads nowhere and a rule on the id draws nothing. The stack builds a
  Positioned's anchor itself, without passing the node through the funnel that writes every node's
  key, bookmark and origin, on the server and in the browser alike (#940).

## What Changes

- A cell is written `min-width: 0` or `min-height: 0` only on an axis the stack caps its layer on,
  where Photon measures the layer against the stack's extent: an axis the child fills, and an axis a
  scroller hugs. On any other axis the layer counts toward the stack's minimum.
- `Fills` answers for a `ScrollView`, on the server and in the twin, so a wrapper around a filling
  scroller stretches for it, as it does around any other filling child.
- The anchor a `Stack` builds for a `Positioned` carries the Positioned's key, bookmark and origin,
  byte-identical across hydration.

For a developer: a hugging stack keeps its content's size in a tight row, as it does on Photon, and a
`Bookmark` on a `Positioned` is a place a link can reach. Nothing changes on Photon. The workaround
#940 records, a bookmark on the node inside the `Positioned`, keeps working. No public surface moves.

## Capabilities

### Modified Capabilities

- `layout`: a stack caps a layer only where Photon does; a Positioned's anchor carries its key,
  bookmark and origin.

## Impact

- `src/eQuantic.UI.Web/WebLoweringVisitor.Containers.cs` and `WebLoweringVisitor.cs`, and the twin in
  `src/eQuantic.UI.Runtime/src/shared/lowering.ts`.
- `component-parity.fixture.json`: the cells of a fixed layer lose the two declarations.
