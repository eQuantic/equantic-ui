# Tasks

## 1. One rule on every target

- [x] 1.1 `BuildOwner.Build` takes the window's width and `ChildSlots` holds `ResolveWidth`'s arm; `LayoutContext.ArmOf` resolves by `WindowWidth`; `AdaptiveNode.Resolve` and `LayoutContext.SizeClass` go. Verify: `S6AdaptiveTests.ANodesOwnThreshold_PicksTheSameArm_InTheElementTree_OnPhoton_AndInTheWebsGates`, a node with `ExpandedFrom = 1024` at 900 dp and the six rows of #735, the element tree's held arm, Photon's paint and the web's gates agreeing; five of the seven fail with the size-class rule put back
- [x] 1.2 The runtime's `AdaptiveNode` twin answers `resolveWidth` with the same rule

## 2. Documentation

- [x] 2.1 The Upgrading page, English and Portuguese, says what replaces `Resolve` and the old `Build`
- [x] 2.2 One entry for the ledger citing #1465 and #735, as a file of its own under `docs/ledger/`
