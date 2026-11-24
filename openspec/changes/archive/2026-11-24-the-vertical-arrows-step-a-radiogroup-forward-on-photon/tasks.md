# Tasks

## 1. The table

- [x] 1.1 `AdjustableKeys.Step(key, role)`, beside `NavigableKeys`, with its line in the per-change API file
- [x] 1.2 The web realizer and Photon's host read it

## 2. Proof

- [x] 2.1 ArrowDown and ArrowUp, ArrowRight and ArrowLeft on a radio group and a tab strip choose the next and the previous item on the web and on Photon; the Photon cases fail with the old mapping
- [x] 2.2 ArrowUp raises a slider's value on both targets
- [x] 2.3 The table steps by role and lets every other key through
- [x] 2.4 The table is pinned against the runtime's `lowerAdjustable` (`adjustable-keys.fixture.json`, written by `AdjustableKeyTableTests`, read by `adjustable.spec.ts`): a flipped entry fails the runtime's half

## 3. Documentation

- [x] 3.1 One entry under `docs/ledger/`
