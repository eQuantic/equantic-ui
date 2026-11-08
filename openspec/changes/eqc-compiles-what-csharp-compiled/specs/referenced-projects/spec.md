## ADDED Requirements

### Requirement: The build compiles what the C# compiler compiled

For the app and for each project it compiles from source, the build SHALL read the project's evaluated
`Compile` items and parse them with the symbols of its `DefineConstants`, the implicit symbols of its
target framework among them, as the C# compiler did. A host that hands no such list SHALL read the
project's directory, with no symbol defined.

#### Scenario: A branch under #if

- **WHEN** a type of the app answers `debug` under `#if DEBUG`, `net10` under `#elif NET10_0_OR_GREATER`
  and `neither` under `#else`
- **THEN** its module answers `debug` in a Debug build and `net10` in a Release build

#### Scenario: A referenced project's own symbols

- **WHEN** a referenced project defines `PRO` and the app does not, and a type of it reads `#if PRO`
- **THEN** its module takes the `PRO` branch

#### Scenario: A removed file and a linked one

- **WHEN** the app's project removes one of its files with `<Compile Remove>` and links one from outside
  its directory
- **THEN** the removed file's class is no module, and the linked file's class is a module a page imports

#### Scenario: A Release build after a Debug one

- **WHEN** both configurations' generated usings are under `obj` and a Release build runs
- **THEN** the build reads the Release usings alone
