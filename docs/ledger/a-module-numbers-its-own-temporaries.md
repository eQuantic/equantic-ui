- **2026-10-29 · A module numbers its own temporaries, whatever the build emitted before it**: a bug under #849
  ([#1138](https://github.com/eQuantic/equantic-ui/issues/1138)). The emitter reset its converter before a
  component's module and never before a plain class's or a static helper's, so those numbered their temporaries
  after the module written before them (`SheetController` began at `$n88`), kept every earlier module's trees in
  the node cache, and an edit to one class renumbered every twin written after it, which is how two open pull
  requests conflicted in twins neither touched. Every module now starts in one place, `BeginModule`, from a reset
  converter with its own model and class, and the record emitter's module resets the converter it is handed.
  Proved by a plain class, a static helper and a record emitted after one that binds two temporaries, which fail
  against `main` (`$n2`) and match their module emitted alone; nine twins regenerate, by the names of their
  temporaries alone. Proposed and archived through OpenSpec (`transpiler-names`).
