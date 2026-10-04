# Spec Delta

## Purpose

The files the build writes for a tool or a platform to read rather than for a person to edit: a
source map, a web manifest, an asset catalog. Each stays in its format whatever text it carries.

## ADDED Requirements

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
