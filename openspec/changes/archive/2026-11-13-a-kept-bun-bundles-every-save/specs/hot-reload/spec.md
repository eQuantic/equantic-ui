## ADDED Requirements

### Requirement: A served eqc keeps one bun between saves, and a bundle writes what changed

A served eqc SHALL hand every save's bundle to one bun process it starts with the first and keeps for
the life of the server. A bundle SHALL bundle every entry point with splitting, and SHALL write a module,
a chunk or a map only when its text changed, a map also when a map it composes with changed; only the
maps it wrote SHALL be composed, and the files the bundle did not produce SHALL be removed after it has
written. A bundle with no maps SHALL be the bytes bun's command line writes with the same flags.

#### Scenario: A second bundle of what did not change

- **WHEN** a kept bundler bundles two pages that share a module, and bundles them again unchanged
- **THEN** the second bundle writes no file and removes none

#### Scenario: One page changed

- **WHEN** one of the two pages changes between two bundles
- **THEN** that page's module is the one file written again, and the other page and the chunk they share
  keep their text and their time

#### Scenario: A TypeScript map changed under the same text

- **WHEN** a module's TypeScript is the same text with another map between two bundles
- **THEN** the module's map is composed again and leads to where the C# is now

#### Scenario: What a publish copies

- **WHEN** a bundle writes no maps
- **THEN** every file it writes is the bytes bun's command line writes with the flags eqc handed it,
  under the same names
