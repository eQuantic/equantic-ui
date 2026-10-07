# generated-files Specification

## Purpose
The files the build writes for a tool or a platform to read rather than for a person to edit: a
source map, a web manifest, an asset catalog. Each stays in its format whatever text it carries.

## Requirements

### Requirement: A JSON file the build writes is JSON whatever text it carries

Every JSON file the build lays out SHALL escape each string it writes, a member's name and a value
alike, so that a reader of JSON reads back exactly the text written: every control character JSON
refuses raw, a quote and a backslash included, whatever C# file, app name or path the text comes from.

#### Scenario: A C# file holding every control character

- **WHEN** a component whose source holds, in a comment, every control character a C# file can hold
  (all but the line breaks) is compiled with its C# carried in its map
- **THEN** the map is JSON, and its `sourcesContent` reads back as the source, where the first raw
  control made the map a file no JSON reader opens

#### Scenario: A file named with a quote

- **WHEN** a component in `Screens/Say"Hi.cs` is compiled with a map
- **THEN** the map is JSON, and its `sources` names `Screens/Say"Hi.cs`

#### Scenario: An app name holding a tab and a carriage return

- **WHEN** the web icons are written for an app named with a tab, a carriage return and quotes
- **THEN** `site.webmanifest` is JSON, and its `name` and `short_name` read back as the app's name

#### Scenario: Every control character through the JSON writer

- **WHEN** a document is written whose member's name and value hold the 32 controls, a quote, a
  backslash, U+2028, U+2029 and a character outside the basic plane
- **THEN** the document is JSON, the name and the value read back as written, and no control is
  written raw

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

#### Scenario: A rebuild under dotnet watch

- **WHEN** a component two pages share is edited under `dotnet watch`, so eqc writes a chunk under a new
  name
- **THEN** `dotnet watch` stays up and the page reloads with the edit

#### Scenario: The compiler off

- **WHEN** an app turns `EnableEQuanticUICompilation` off
- **THEN** the folder is not excluded, and its files are Content as any file under wwwroot is
