- **2026-11-12 · A save after eqc died never hangs**: a save that finds eqc dead releases it outside the compiler's
  gate, where releasing it under the gate waited on the process's exit event while that event waited on the gate, and
  the hot reload hung for good; CI met it as a Server suite that stopped for 15 minutes
  ([#1491](https://github.com/eQuantic/equantic-ui/issues/1491)). Proposed and archived through OpenSpec
  (`openspec/specs/hot-reload`).
