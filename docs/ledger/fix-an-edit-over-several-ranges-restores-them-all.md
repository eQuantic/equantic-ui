- **2026-11-12 · An undo and a redo of a step over several ranges bring every range back**: with B2 and
  D4:D5 selected, Delete emptied the three cells as one step, and undo gave the values back but left D4:D5
  alone selected ([#1100](https://github.com/eQuantic/equantic-ui/issues/1100)). An edit recorded the active
  range alone, and undo and redo put the active cell at its anchor. It now records a `SheetSelection`, every
  range with the active one last and the active cell, so Delete and Ctrl+Enter over several ranges undo
  and redo with their ranges, and an active cell Tab walked inside its range comes back where it was, on
  both targets and in the twin. A paste puts the active cell where it landed, as Excel does. Proposed and
  archived through OpenSpec (`openspec/specs/spreadsheet`).
