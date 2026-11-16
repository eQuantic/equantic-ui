- **2026-11-16 · The samples job stops on its own**: the `samples` job stops at 60 minutes, and its edit-to-view step
  at 15, where three merge groups held a macOS runner an hour each, the step sitting until the runner died with no log
  ([#1473](https://github.com/eQuantic/equantic-ui/issues/1473), which stays open for why the desktop edit is not drawn).
  Proposed and archived through OpenSpec (`openspec/specs/working-agreement`).
