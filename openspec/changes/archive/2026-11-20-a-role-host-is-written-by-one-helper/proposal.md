# Proposal

Closes #1517, a Task under #190 (Flutter parity: the gaps that are work). Found by the review of #677,
whose change is archived as `2026-11-13-a-select-groups-its-options`.

## Why

Six wrappers of the vocabulary put one element between a parent and its child to carry semantics:
`Progress`, `Adjustable`, `LiveRegion`, `Group`, `MergeSemantics` and `ExcludeSemantics`. Each host
obeys one rule, the box a reader outlines is the box it names, so it takes its child's width contract
(`fit-content` or `100%`, the child's cap as `max-width`, `100%` height on a filling child). On each
producer, the C# `WebLoweringVisitor` that renders on the server and the runtime's `lowering.ts` that
renders in the browser, `MergeSemantics` and `ExcludeSemantics` already share one host
(`SemanticsHost`, `semanticsHost`), and the four others write the same five lines by hand.
`RoleBearingHostBoundsTests` and `role-host-bounds.spec.ts` exist because those hand-written copies
once disagreed (#239, #241). A seventh wrapper copied by hand is the next divergence.

## What Changes

- On each producer, the existing semantics host takes the parts that really differ between the six:
  the role's attributes, the accessible name, and whether the host takes the pointer. It hands back the
  host with the width contract written once. `SemanticsHost(child)` becomes
  `SemanticsHost(child, attributes, label, takesPointer)` in C#, and `semanticsHost(child)` becomes
  `semanticsHost(child, attributes, label, takesPointer)` in TypeScript.
- `LowerProgress`, `LowerAdjustable`, `LowerLiveRegion` and `LowerGroup`, and their TypeScript twins,
  call it in place of their own copy. `LowerMergeSemantics` and `LowerExcludeSemantics` pass no role
  attributes and no name. What each wrapper adds after the host (a value, an orientation, a key
  handler, a merge's reading, `aria-hidden`) and the lowering of its child stay in the wrapper.
- The two bounds suites gain the rows for `MergeSemantics` and `ExcludeSemantics`, so they hold all six
  wrappers that call the helper.
- Nothing a developer using the SDK writes, sets or sees changes: every element, attribute, class and
  style the two producers write is the same, byte for byte, and every pin holds.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none: no requirement moves. The change is private to the two producers, so it sets `skip_specs`.)

## Impact

- **Web realizer** (`src/eQuantic.UI.Web/WebLoweringVisitor.Semantics.cs`,
  `WebLoweringVisitor.Interaction.cs`): private methods only.
- **Runtime** (`src/eQuantic.UI.Runtime/src/shared/lowering.ts`): module-private functions only. The
  served runtime's size moves by a few bytes down, and no budget delta is owed.
- **Tests**: two rows in `RoleBearingHostBoundsTests` and in `role-host-bounds.spec.ts`.
- **Not reached**: eqc, Photon (its `MeasureWrapper` already gives a wrapper its child's bounds), the
  SDKs, the templates, the wiki.
- **Public surface**: none. No `PublicAPI.Unshipped*.txt` moves, and
  `tests/eQuantic.UI.Web.Tests/developer-surface.baseline.txt` does not move.
- **Break**: none, so no migration line.

## Principles

- **9, the structural fix**: one writer of the host on each producer, in place of a suite that compares
  hand-written copies after they diverge. The suites stay, as the net.
- **4, written once, realized per target**: the two producers stay twins, with the same helper under
  the same name and the same inputs, cross-cited.
- **11, object orientation over static helpers**: the C# helper stays an instance method of the
  visitor, beside `Fills` and `CapsAt`, which it reads. No `XxxHelper` class.
- **13, in preview, leave nothing behind**: the old one-argument shape goes, and every caller is
  changed in the same change. Nothing public is reached.
- **6, Flutter's answer**: Flutter's `Semantics` is one proxy over any child, sized by it
  (`RenderSemanticsAnnotations` is a `RenderProxyBox`, in the third tree). The web's host is that proxy
  written into the DOM, so there is one writer of it, under the name Flutter uses.
