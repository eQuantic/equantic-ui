# Tasks

## 1. The floor

- [x] 1.1 Add `Touch.MinPointerTarget` (24) beside `Touch.MinTarget`, and read it in the three places that keep the hit contract: `Sizing.HitTarget(size, Density.Compact)`, Photon's `ExpandHitRect` and the web's fine-pointer gate (`Tokens.cs`, `EmitVisitor.Interaction.cs`, `TokenCss.cs`)
- [x] 1.2 Measure a label-less Checkbox's hit region on Photon under both densities (`DensityTests`): 24 × 24 under `Compact`, 48 × 48 under `Comfortable`; the Compact case fails against main's `ExpandHitRect`, at 20
- [x] 1.3 Pin each pointer's gate and its minimum in `HitSlopTests`

## 2. The web's slop under the content

- [x] 2.1 Measure the slop in a browser on the dashboard sample, under a fine pointer, with the same script on each side: with the slop over the content (the floor as first written) the Search button's centre hit the button element, its box never matched `:hover` and its fill stayed at rest, and the Notifications and Help buttons inside a Pressable were hit as the wrapper; with no fine-pointer rule (main) every centre hit the content and the fill showed
- [x] 2.2 Make the slop the `::before` and lift the pressable's children to its level with no specificity (`:where(.eq-pressable) > * { position: relative; }`), under both pointers (`TokenCss.cs`)
- [x] 2.3 Pin it in `HitSlopTests`: each gate lifts the content and no slop is an `::after`; five of its six cases fail against the slop over the content
- [x] 2.4 Pin what the deleted `APointerDeviceIsLeftAlone` held (found in review): one slop per pointer, each inside its gate, and never the finger's minimum in the fine gate; it fails against a slop outside both gates, which every other case lets through, and against the finger's minimum in the fine gate
- [x] 2.5 File the hole the lift leaves behind a child that draws no box (`display: contents`) as #622, and the density a served page keeps until each component re-renders, found while measuring, as #623
- [x] 2.6 Measure it again in the same browser and with the same script: under a fine pointer every button's centre hits its content, the Search button shows its hover fill, and a point 1px above the label-less Checkbox's 22px box hits the checkbox; under a coarse pointer (mobile emulation) every centre hits its content, where main's rule took three of the first four, and the Checkbox is hit 12px above its box and not 14px above it

## 3. The handoff

- [x] 3.1 Publish the floor at `touch.minPointerTarget` in `docs/design/tokens.json`, compared by `HandoffTokenPinTests` and counted by `HandoffSdkCoverageTests`, and regenerate the runtime's `design-system.generated.ts` (`EQ_UPDATE_DESIGN_TS=1`)
- [x] 3.2 Say the floor shipped in Foundations §08, `controlMetrics.compactHitDecision`, `touch.note` and `proposals.json`, mark the two figures §08 prints with `touch.minPointerTarget`, and raise `HandoffFigureTests`' floor from 52 to 54
- [x] 3.3 Repoint the fidelity audit's citations to the lines that moved, re-quoting the two that cite the Compact minimum

## 4. Documentation and the suites

- [x] 4.1 The wiki's DesignSystem, Photon and WriteOnceComponents pages, English and Portuguese, on the wiki branch of this pull request
- [x] 4.2 `docs/LEDGER.md`: one line for this event citing #430
- [x] 4.3 The suites, each alone and read by its exit code: Web, Native.Engine, Server, Design, Compiler, Conformance and the runtime's `TestRuntime`
