# Proposal

Closes #885, a Bug under #160 (Instruments that fail, not warn).

## Why

The wiki reads in five languages since its commit 7eb1524: English at its root, and Portuguese,
Spanish, French and German under `locale/<culture>/<Page>-<culture>.md`. The guards that read it
knew two. `WikiVocabularyTests` allowed a retired spelling by file name, one entry per language, so
the German, Spanish and French copies of `PackageArchitecture`, which tell the same history of
`eQuantic.UI.Core` the English and Portuguese ones are allowed to tell, failed the `test` job of
every pull request whose wiki branch is master. `WikiDiagnosticsTests` and
`WikiShellRealizationsTests` read the English and the Portuguese page by name, so a translation of
either page would drift with nothing to see. And the working agreement says a page changes in
English and Portuguese.

## What Changes

- `WikiClone` answers every copy of a page, by its language, and the page a file is a copy of.
- An allowance of `WikiVocabularyTests` names the English page and covers every translation of it,
  and is stale only when no copy carries the word.
- `WikiDiagnosticsTests` and `WikiShellRealizationsTests` read every language the page is written in,
  the English and the Portuguese page still named as the contract. The Capabilities guard reads a
  language by the word its table writes for an unavailable contract, and fails on a language it has
  no word for, rather than leave it unread.
- The Workflow section of `CLAUDE.md` and `AGENTS.md`, and the pull request template, say a wiki page
  changes in every language it is written in, and a new page is written in all five.

For a developer who uses the SDK: nothing changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `working-agreement`: a pull request carries its wiki pages in every language the wiki reads them
  in, and every docs guard reads each of those languages.
