## 1. The engine's count

- [x] 1.1 `ElementIds`, and an optional count on `BuildOwner`'s constructor and `Structural`, with their PublicAPI lines
- [ ] 1.2 The twins regenerated (`EQ_UPDATE_TRANSPILED=1` on SharedComponentTranspilationTests, through the lock). Verify: ElementTreeTests and ElementEngineTwinTests pass
- [ ] 1.3 Every owner the browser makes takes the document's count (after E3b-1's owners land)

## 2. State on the position

- [x] 2.1 `node-states.ts`, the twin of Photon's `NodeStates`, and `positionOf`
- [x] 2.2 The scroll, table, in-view and canvas after-passes keep their state on the position, with handover
- [x] 2.3 The code surface's id and reveal, the sheet's id, the focus scope, the options list, the anchored panel and the canvas key
- [ ] 2.4 The lowering's path threading removed once nothing reads it (after E3b-1)

## 3. Proof

- [x] 3.1 A spec per scenario of this change (`node-states.spec.ts`) and the C# count test
- [ ] 3.2 The runtime's suites, with the specs that read a path attribute reading the position's id
- [ ] 3.3 The e2e suite in a real browser
- [ ] 3.4 The served runtime's budget, re-recorded in a commit of its own if it moved
- [ ] 3.5 `docs/ELEMENT-TREE-PLAN.md` and one ledger entry citing #1454
