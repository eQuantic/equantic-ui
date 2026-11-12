# Tasks

## 1. A mark of no width draws nothing

- [x] 1.1 `CodeBlock.Marks` skips a mark whose width is 0, the hint's dots aside. Verify: `UnmeasuredGeometryTests.AProblemsWave_OnTheServer_DrawsNothingRatherThanFailTheBlock` lowers a block holding a ranged and an empty problem through the server's path, and before the change the server drew "This section could not be displayed." in its place
- [x] 1.2 The sample. Verify: the dashboard sample's `/code`, run in Production, logs no contained failure

## 2. Documentation

- [x] 2.1 One entry for the ledger citing #1367, as a file of its own under `docs/ledger/`
