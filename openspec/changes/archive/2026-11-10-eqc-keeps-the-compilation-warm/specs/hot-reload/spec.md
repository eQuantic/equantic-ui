## ADDED Requirements

### Requirement: A served eqc keeps the project's compilation between saves

A build of the eqc the server keeps running SHALL start from the compilation the build before it
loaded, replacing in place the tree of each file whose text changed, and SHALL write exactly what a
cold build writes after the same edit. A build SHALL load cold when a file was added or removed, when
an input the SDK handed changed, or when a file of a project the app references changed.

#### Scenario: A file's text changed

- **WHEN** a save changes a constant one file declares and another file's component reads
- **THEN** the warm build writes the component's module with the new value, byte for byte what a
  cold build writes

#### Scenario: A file added

- **WHEN** a file was added since the last build
- **THEN** the build loads cold, and writes the new file's module

#### Scenario: A referenced project's file changed

- **WHEN** a file of a project the app references changed
- **THEN** the build loads cold
