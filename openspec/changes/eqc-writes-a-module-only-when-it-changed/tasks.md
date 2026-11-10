# Tasks

## 1. Write what changed

- [x] 1.1 Write a module, its map and a catalog only when the text differs from the file's. Verify: a save that changes one constant writes the module that inlines it and leaves another module's time as it was, and the case fails when every file is written
- [x] 1.2 A catalog that keeps its text keeps its time. Verify: the catalogue case

## 2. eqc prunes by what it produced

- [x] 2.1 Remove the catalogs the compile did not name. Verify: a culture taken out leaves no catalog, and the case fails without the prune
- [x] 2.2 Remove the modules, chunks and maps bun did not write, never the runtime. Verify: `ModuleBundlerMapsTests` through bun, and the case fails with the prune back to maps only
- [x] 2.3 Remove the SDK's prune by a file's time. Verify: `EqcOutputIsBuildOutputTests` finds no target reading a file's time over eqc's folder

## 3. The proof

- [ ] 3.1 The compiler's and the conformance suites pass
- [ ] 3.2 The measurement on the dashboard sample, with the load average beside each number
- [ ] 3.3 CI's samples job: a publish right after a shared component changes, and a Release publish with no map

## 4. Documentation

- [x] 4.1 One entry for the ledger citing #1279, as a file of its own under `docs/ledger/`
