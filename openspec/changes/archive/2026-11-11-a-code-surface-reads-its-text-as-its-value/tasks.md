# Tasks

## 1. The reading

- [x] 1.1 `TextReading` and `ICodeSurfaceModel.Reading` in the vocabulary; `CodeEditorController.Reading` (the lines around the caret), `SheetFormulaBar.Reading`, `SheetNameBox.Reading`. Verify: `CodeReadingTwinTests`, the same reading from the C# engine and its twin over a real file, and the window and the offsets checked by hand

## 2. The realizers

- [x] 2.1 The web's door holds the reading (code) or the announcement (sheet), its value and selection kept after each render, the drawing `aria-hidden`; Photon's code field takes the reading's text as its value. Verify: `SurfaceSsrTests.ItsInputHoldsTheReading_AndItsDrawingIsHidden`, the runtime's "a reader hears the field and its text as one thing", and `SemanticsParityTests` with the NameBox entry removed and the four spreadsheet entries left to #1444
- [x] 2.2 The browser. Verify: the dashboard sample's code editor and spreadsheet, the door's value and selection following the caret

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1333, as a file of its own under `docs/ledger/`
