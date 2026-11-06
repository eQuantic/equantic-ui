# sdk-strings Specification

## Purpose
The SDK's own user-facing strings, every built-in label a component shows or announces: read through `SdkStrings` from `SdkResources` and its culture catalogs, by .NET's fallback on every target, in the languages the SDK ships.

## Requirements

### Requirement: The SDK's own strings speak every culture its catalogs reach

The SDK's built-in strings SHALL be read from `SdkResources` and its culture catalogs through .NET's
own fallback, a culture before its parent and the neutral catalog last, on the server, on native and
in the browser alike. The SDK SHALL ship a neutral `pt` catalog in European Portuguese beside its
`pt-BR` one, so that every Portuguese culture but Brazil's reads Portuguese. Every catalog SHALL hold
each key of the neutral catalog and no other, with the same placeholders, and this repository's test suite SHALL fail when one does not.

#### Scenario: An app in European Portuguese

- **WHEN** a page in `pt-PT` shows a `FileUpload`
- **THEN** it reads "Escolher ficheiros", on the server and in the browser, where it read "Choose files"

#### Scenario: Brazil keeps its own

- **WHEN** a page in `pt-BR` shows the same component
- **THEN** it reads "Escolher arquivos"

#### Scenario: A translation short of a key

- **WHEN** a catalog of the SDK lacks a key the neutral catalog has
- **THEN** `SdkStringsLocalizationTests` fails, naming the culture and the key
