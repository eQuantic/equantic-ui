- **2026-11-10 · A conflict a merge left in a page fails the docs guards**: a Bug under #1174
  ([#1351](https://github.com/eQuantic/equantic-ui/issues/1351)). A merge of the wiki committed git's conflict
  markers into `Upgrading.md` on master and every docs guard passed, since each reads what it is about and none
  whether the page is whole. `ConflictMarkerTests` now reads every page of the wiki in its five languages and every
  Markdown file git tracks here, and fails naming each marker's file and line; seven `=` count only where a title's
  Setext underline cannot stand. Proved on the wiki's commit before its fix, which fails at `Upgrading.md`, and on a
  conflict planted in `docs/README.md`. Proposed and archived through OpenSpec (`working-agreement`).
