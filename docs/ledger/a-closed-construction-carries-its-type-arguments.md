- **2026-11-24 · A runtime library's generic carries its type arguments**: a Bug under #1587
  ([#1863](https://github.com/eQuantic/equantic-ui/issues/1863)), found by #1563. eqc marked a closed
  construction with its type arguments only when the source declared its generic type, and an app reads the
  framework from metadata, so `new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(…)` crossed unmarked and
  the browser took factories over different types for one runtime type. A generic the runtime library twins is
  marked as one the source declares is; the BCL's collections and constructions whose type arguments are known
  only at run time (#755) stay as they were. `RuntimeGenericClosureTests` reads a library from its image.
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-classes`).
