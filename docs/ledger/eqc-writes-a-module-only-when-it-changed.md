- **2026-11-10 · eqc writes a module only when its text changed, and removes what it stopped
  writing itself**: a compile rewrote every module, map and catalogue on every save, so every file's
  time moved with nothing in it changed. Each is now written only when its text differs from the
  file's. The SDK's prune of eqc's folder read a file older than the compile's start as one the
  compile did not write, which no longer held, and never ran for the eqc a running app keeps. eqc
  removes what it stopped producing itself, after it wrote the rest: the catalogues by the names it
  wrote, the bundle's modules, chunks and maps by what bun wrote. A save that changes one constant
  writes the one module that inlines it
  ([#1279](https://github.com/eQuantic/equantic-ui/issues/1279)). Proposed through OpenSpec
  (`openspec/specs/generated-files`).
