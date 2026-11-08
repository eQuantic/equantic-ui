- **2026-10-31 · An undo over several ranges gives every range back**: a Bug ([#1100](https://github.com/eQuantic/equantic-ui/issues/1100)).
  Delete and Ctrl+Enter over several ranges are one undo step, and the step recorded the active range alone, so
  an undo brought every cell back and selected one range, with the active cell on its anchor. An edit records
  `SheetSelection` now, every range and the active cell, and an undo and a redo put all of it back, on both
  targets through the transpiled engine. `SheetEdit.SelectionBefore` and `SelectionAfter` change type, and
  `SheetController.Fill` takes one. Proposed and archived through OpenSpec (`spreadsheet`).
