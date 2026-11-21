- **2026-11-19 · An issue the board adds at the same moment still gets its points**: `scripts/new-issue.py`
  makes its add to Project 11 again when GitHub refuses it because the board's own workflow added the
  issue at the same moment, and sets the status and the points on the item it answers, where it left the
  issue without its points and said it was not whole (#1645, one of the 127 issues of the 3D epic). Adding
  an issue the board holds answers its item, measured, so a second add is safe. A test runs the script
  whole against a board in memory, in CI's `roadmap` job
  ([#1752](https://github.com/eQuantic/equantic-ui/issues/1752)). Proposed and archived through OpenSpec
  (`openspec/specs/working-agreement`).
