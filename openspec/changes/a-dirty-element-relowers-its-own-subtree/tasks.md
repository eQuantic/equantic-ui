## 1. What a component lowered to is the position's

- [x] 1.1 `lowered-records.ts`: the record, its snapshot, the reads that mark it, and the checked replay
- [x] 1.2 `lowerNode` records a component's lowering with its inputs and the walk's ambients
- [x] 1.3 Every read of a lowered child marks it (`atomicDeclaration`, `asksForAll`, `numberItemRows`, the shortcut's marker)

## 2. A render the owners asked for is answered by patches

- [x] 2.1 `relowerBuilt`: build the root through its owner, collect the positions it built, lower the topmost patchable records alone
- [x] 2.2 `RenderManager.patch`: reconcile one subtree and put the new root in the current tree
- [x] 2.3 `patchPage` before the scheduled render, falling back to the whole render on any refusal
- [x] 2.4 Shortcuts carry the lowering that declared them, and a partial pass replaces only theirs

## 3. Proof

- [x] 3.1 `subtree-relowering.spec.ts`: one lowering for one component, two for two, the outer for a nested pair, a parent's change replayed, the page for a parent that reads its child, and the shortcuts kept; every patched DOM equal to the whole lowering's
- [x] 3.2 The runtime's suites: 3025 of 3026, the one left a perf spec's timing assert at a load average past 300 that fails on E3b-1's head too; no .NET code moved, so the twins are untouched
- [x] 3.3 The e2e suite in a real browser: 16 of 16 on the dashboard sample
- [x] 3.4 The served runtime's budget, re-recorded in a commit of its own: 332,177 bytes gzipped, about 2.3 KB of it this slice's
- [ ] 3.5 `docs/ELEMENT-TREE-PLAN.md` and one ledger entry citing #1455
