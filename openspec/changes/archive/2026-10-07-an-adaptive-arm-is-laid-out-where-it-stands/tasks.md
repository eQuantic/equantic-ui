# Tasks

## 1. A gate names a fractional threshold a selector can reach (#669)

- [x] 1.1 Measure it in Chromium with the rules the realizer wrote: with the dot, every style rule of both gates is dropped (two empty `@media` blocks remain), `querySelector` refuses the selector, and both arms compute `display: block` at 0 and at 1024px; with an underscore, one arm shows at a time
- [x] 1.2 Measure what each producer spells (`dotnet fsi` and node): C#'s `"0.####"` of a float rounds to seven significant digits, 1066.6667 to `1066.667` and 1279.9999 to `1280`, where the TypeScript twin writes `1066.6667` and `1279.9999`
- [x] 1.3 Spell the threshold in ten-thousandths of a dp, an underscore in the name and a dot in the media condition, and read it back from the name, in `AdaptiveGates` and in `style-atomizer.ts`, the runtime's question whether an arm is shown included
- [x] 1.4 Pin it on both twins (`S6AdaptiveRealizerTests`, `s6-adaptive.spec.ts`): the names and rules of a fractional gate, three thresholds past a thousand, and the gate read back at 703 and 704px; on the code before the fix (44e31df8), the 4 C# cases and the 5 TypeScript ones fail

## 2. An arm is laid out by the parent it stands in (#670, #671)

- [x] 2.1 Measure both defects on the three realizers: on the web a Gap arm in a Column lowers to nothing and a Positioned arm in a Stack degrades into its cell, in both twins; on Photon the Gap arm takes no space (the box below sits at 10 where 34 and 74 are due) and the Positioned arm lands at 0 where 368 is due
- [x] 2.2 Place each arm by its parent's rule for a direct child, inside its gate, in both twins (`Place` and `placeChild`): a flex's axis and align-self, a grid's span, a stack's anchor or cell after resolving a component; the AdaptiveNode's own door lowers its arms on the axis it was given, and the wrapper keeps the node's key, bookmark and origin (`Decorate`, `decorate`)
- [x] 2.3 On Photon, read the resolved arm in an AdaptiveNode's place in the line, the wrapping line and the grid (`LaidOutChildren`, `LayoutContext.ArmOf`), at the path the node's own measurement gives it, and ask the measured node first in `PositionedOf`; measure a child the wrapping line grows again at its first path
- [x] 2.4 Pin it on the three realizers: a Gap arm in a Column and in a Row, a Positioned arm in a Stack, and an arm's own align-self and span (`S6AdaptiveRealizerTests`, `s6-adaptive.spec.ts`, `S6AdaptiveTests`), and the wrapping line's path (`FlexBasisWrapLayoutTests`); on the code before the fix, 3 C# cases, 3 TypeScript ones and 5 native ones fail
- [x] 2.5 Say on `AdaptiveNode` that the arm, not the node, carries `AlignSelf` and `GridSpan`
- [x] 2.6 Measure it in Chromium on the server's own output (`WebRealizer.Lower` and `HtmlRenderer`) of the issues' three trees, with the same script on each side: before the fix the Gap arm leaves 0px between "above" and "below" at 703 and at 1100px, "narrow" and "wide" both show at both widths, and "corner" sits at the stack's start; with the fix the gap is 24px at 703 and 900px and 64px at 1100px, one arm shows at a time on either side of 703.7037px, and "corner" is hidden at 703, 704 and 900px and at the top end corner at 980 and 1100px
- [x] 2.7 The perf harness, before and after: 73.2 KB a pooled frame before and 71.1 after, 73.2 again with only the line's `foreach` over its node put back (it boxes the list's enumerator), and 506 bytes a layer throughout

## 3. Documentation and the suites

- [x] 3.1 `docs/LEDGER.md`: one line for this event citing #669, #670 and #671
- [x] 3.2 Repoint the 74 citations of `docs/HANDOFF-FIDELITY-AUDIT.md` into the files that moved, by the diff from the base so each keeps quoting the code it quoted, `DocsIndexTests` having found 24 of them stale; the three on rewritten lines by hand: the two in `LowerStack` to `LowerLayer`, where their code went, and one already stale on the base (`lowering.ts:2182`, a one-line Text's `display: block`) to the line its prose describes
- [x] 3.3 No wiki page documents a gate's name or how an arm is placed, and what DesignSystem, WriteOnceComponents, Components, GettingStarted and Roadmap say of AdaptiveNode still holds
- [x] 3.4 The classes near the change, each run read by its exit code and its total: 191 web tests in 28 classes, 177 native ones in 26, and the runtime's `TestRuntime` (tsc, then 2,165 vitest tests in 163 files); the full suites are the pull request's
