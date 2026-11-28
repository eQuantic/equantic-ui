## 1. An anchored panel stays inside the viewport (#1933)

- [x] 1.1 `Anchored.ViewportInset`, and the placement's docs without the clamp fence
- [x] 1.2 Photon: the realizer moves a laid-out panel back inside (`KeepInside`, `LayoutNode.Offset`)
- [x] 1.3 Web: the runtime measures and moves the panels after a pass, on reach and on resize (`dom/anchor-inset.ts`)
- [x] 1.4 `DatePicker` hangs its calendar `BottomEnd`

## 2. Proof, documentation and the suites

- [x] 2.1 `AnAnchoredPanelStaysInTheViewportTests` at 390, 768 and 1280, failing without the change
- [x] 2.2 `anchor-inset.spec.ts`, and a panel measured in a browser at 330, 768 and 1280
- [x] 2.3 A ledger entry under `docs/ledger/`
- [x] 2.4 Every test project of the solution with the Conformance suite in full, each read by its exit code
