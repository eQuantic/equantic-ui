## 1. The engine's count

- [x] 1.1 `ElementIds`, and an optional count on `BuildOwner`'s constructor and `Structural`, with their PublicAPI lines
- [x] 1.2 The twins regenerated (`EQ_UPDATE_TRANSPILED=1` on SharedComponentTranspilationTests, through the lock). Verify: ElementTreeTests and ElementEngineTwinTests pass
- [x] 1.3 Every owner the browser makes takes the document's count (`liveOwner`, `structuralOwner`)

## 2. State on the position

- [x] 2.1 `node-states.ts`, the twin of Photon's `NodeStates`, and `positionOf`
- [x] 2.2 The scroll, table, in-view and canvas after-passes keep their state on the position, with handover
- [x] 2.3 The code surface's id and reveal, the sheet's id, the focus scope, the options list, the anchored panel and the canvas key
- [x] 2.4 The lowering's path is read by nothing of this slice's; it stays for the one identity that crosses to the server, a table's and a grid's cell scope (#1419), a seam string as plan 3.8 allows

## 3. Proof

- [x] 3.1 A spec per scenario of this change (`node-states.spec.ts`) and the C# count test
- [x] 3.2 The runtime's suites, with the specs that read a path attribute reading the position's id
- [x] 3.3 The e2e suite in a real browser: 16 of 16 on the dashboard sample
- [x] 3.4 The served runtime's budget: inside it, nothing re-recorded
- [x] 3.5 `docs/ELEMENT-TREE-PLAN.md` and one ledger entry citing #1454
