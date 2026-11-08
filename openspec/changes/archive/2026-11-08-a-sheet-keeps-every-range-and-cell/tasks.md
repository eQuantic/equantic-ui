# Tasks

## 1. Undo over several ranges (#1100)

- [x] 1.1 Record every range and the active cell in `SheetSelection`, and restore them on undo and redo
- [x] 1.2 Transpile the twin again and drive it in `sheet-model.spec.ts`
- [x] 1.3 Prove it both ways: `AnUndoAndARedoOfAStepOverSeveralRanges_GiveEveryRangeBack` fails with the active range alone, and with every range but the anchor as the active cell; the twin's case fails against the twin as it stood

## 2. A reader's activation (#1116)

- [x] 2.1 Add `ISheetSurfaceModel.Activate(row, column)` and answer it in `SheetController`
- [x] 2.2 Call it from the web's click and from `PhotonHost.Activate`, by the cell's place
- [x] 2.3 Prove it both ways: a row half under the headers is made active on both targets, and selects its column against the hosts as they stood

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` per Bug; the wiki states no gap here, so no page changes
