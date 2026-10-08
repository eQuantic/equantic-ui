# Spec Delta

## ADDED Requirements

### Requirement: eqc's output folder is build output, replaced in place

The web SDK SHALL declare eqc's output folder in `DefaultItemExcludes` while the compiler is on, so
that no default glob takes its files and `dotnet watch` ignores what eqc writes there. A compile SHALL
NOT empty the folder before eqc writes it: eqc SHALL write its files in place, and the files it did
not write (a removed component's module, a renamed chunk, a culture taken out, the maps a Release
build does not write) SHALL be removed after it has written, never the runtime another target writes.

#### Scenario: A component removed between two compiles

- **WHEN** the output folder holds a module, a map and a culture's catalog from an earlier compile that
  this one does not write, beside the runtime and a module it rewrites
- **THEN** while eqc runs the folder still holds them all, and after it the three it did not write are
  gone and the runtime and the rewritten module remain

#### Scenario: A bundle that fails

- **WHEN** bun refuses a module while the folder holds the maps an earlier compile wrote
- **THEN** the maps are still there as they were; a bundle that succeeds removes the ones it did not
  write only after bun has written, and composes only the ones bun wrote

#### Scenario: A rebuild under dotnet watch

- **WHEN** a component two pages share is edited under `dotnet watch`, so eqc writes a chunk under a new
  name
- **THEN** `dotnet watch` stays up and the page reloads with the edit

#### Scenario: An app that includes wwwroot by hand

- **WHEN** an app turns the default content items off and includes `wwwroot/**` itself
- **THEN** eqc's output folder is still not Content

#### Scenario: The compiler off

- **WHEN** an app turns `EnableEQuanticUICompilation` off
- **THEN** the folder is not excluded, and its files are Content as any file under wwwroot is
