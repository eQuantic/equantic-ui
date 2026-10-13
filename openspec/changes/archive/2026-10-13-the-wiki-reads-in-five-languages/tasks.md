# Tasks

## 1. The guards

- [x] 1.1 `WikiClone.Copies` answers a page's copies by language, and `WikiClone.PageOf` the page a file is a copy of
- [x] 1.2 `WikiVocabularyTests`: an allowance names the English page and covers its translations; it is stale only when no copy carries the word
- [x] 1.3 `WikiDiagnosticsTests` and `WikiShellRealizationsTests` read every copy of their page, English and Portuguese required, and the Capabilities guard fails on a language it has no word for
- [x] 1.4 Run the guards against the wiki's master, which failed before, and against a copy of it where a translation breaks each guard: a retired spelling on a page with no allowance, every copy of an allowed page without the word, a German Diagnostics page short of a row, a French Capabilities page

## 2. The agreement

- [x] 2.1 The Workflow section of `CLAUDE.md` and `AGENTS.md`, and the pull request template, name the five languages
- [x] 2.2 One `docs/LEDGER.md` line citing the issue
- [x] 2.3 Archive the change with `openspec archive the-wiki-reads-in-five-languages --yes`
