- **2026-11-16 · A save that is the first to see eqc dead never hangs**: a save asks the running eqc whether it
  exited, and releases it, with the compiler's gate free. Asking it under the gate raised the process's exit event when
  nothing had seen the exit yet, and that event waited on the gate while the save waited on the event, so the hang
  #1496 meant to cure kept stopping the Server suite for 15 minutes on CI, and ejected a pull request from a merge group
  ([#1491](https://github.com/eQuantic/equantic-ui/issues/1491)). Proposed and archived through OpenSpec
  (`openspec/specs/hot-reload`).
