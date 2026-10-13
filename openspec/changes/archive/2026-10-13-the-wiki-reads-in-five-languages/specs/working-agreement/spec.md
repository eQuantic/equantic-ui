# Spec Delta

## ADDED Requirements

### Requirement: A docs guard reads every language a page is written in

A docs guard that holds a wiki page to the tree SHALL read every copy of the page the wiki has: the
English page at its root and each translation at `locale/<culture>/<Page>-<culture>.md`. The English
and the Portuguese page SHALL stay named as the contract, so that a rename that lost one fails rather
than leave the guard reading nothing. A guard that reads a language by its words SHALL fail on a
language it has no words for, rather than leave the page unread. An allowance to carry a retired
spelling SHALL name the English page and cover every translation of it, and SHALL be stale only when
no copy of the page carries the word.

#### Scenario: A translation tells the history its page is allowed to tell

- **WHEN** the German, Spanish and French copies of `PackageArchitecture` name `eQuantic.UI.Core`, which the English page is allowed to name
- **THEN** `WikiVocabularyTests` passes, where it failed every pull request reading the wiki's master

#### Scenario: A translation short of a row

- **WHEN** the wiki has a German Diagnostics page without a code `docs/DIAGNOSTICS.md` documents
- **THEN** `WikiDiagnosticsTests` fails, naming `de` and the code

#### Scenario: A language the guard cannot read

- **WHEN** the wiki has a French Capabilities page and the guard has no French word for an unavailable contract
- **THEN** `WikiShellRealizationsTests` fails, naming `fr` and the words it lacks

## MODIFIED Requirements

### Requirement: The wiki's master moves with main

A pull request that changes what the wiki must say SHALL carry its pages in every language the wiki
reads them in, English, Portuguese, Spanish, French and German, a new page in all five, in one commit,
on a wiki branch named like its own branch, and that branch SHALL be merged into the wiki's master
when the pull request merges, and not before.

#### Scenario: A pull request adds a diagnostic

- **WHEN** a pull request adds a diagnostic code and carries its wiki rows on its wiki branch
- **THEN** its own guards pass, and every other pull request's guards, which read master, still
  pass without the rows

#### Scenario: The pull request merges

- **WHEN** the pull request merges into main
- **THEN** its wiki branch is merged into the wiki's master and deleted
