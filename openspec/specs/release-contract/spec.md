# release-contract Specification

## Purpose
TBD - created by archiving change what-1-0-promises. Update Purpose after archive.

## Requirements

### Requirement: Every shipping assembly has a surface class

Every project that ships an assembly SHALL have one surface class: `contract`, what an app compiles
against and the compatibility promise covers, `tool`, an assembly of the SDK's own tools that the
promise does not cover, or `experimental`, an assembly the promise does not cover yet. The class
SHALL be derived from the project's name unless the project sets it: a native project is
experimental, the compiler and the code writers are tools, and every other project is contract. The
class SHALL be written into the assembly as `[assembly: AssemblyMetadata("eQuantic.Surface", class)]`,
so it can be read from what ships.

#### Scenario: The classes the tree holds

- **WHEN** the shipping projects are built
- **THEN** every `eQuantic.UI.Native.*` assembly says `experimental`, `eQuantic.UI.Compiler` and
  `eQuantic.UI.Codegen` say `tool`, and every other one says `contract`

#### Scenario: A new project

- **WHEN** a project that ships an assembly is added and sets no class
- **THEN** it is contract, which is the class the promise covers

### Requirement: The native track is marked experimental

An experimental assembly SHALL carry `[assembly: Experimental("EQX0001")]` with a URL to the wiki
section that says what experimental means in 1.0. An app built with `eQuantic.UI.Sdk.Native` SHALL
NOT see EQX0001, and its publish SHALL say once that the native track is experimental in 1.0. Any other
project that uses a type of an experimental assembly SHALL fail with EQX0001 until it suppresses it.
An experimental assembly that uses another one SHALL NOT see it.

#### Scenario: A Photon app

- **WHEN** an app made by `dotnet new equantic-native` is built and published
- **THEN** it builds with no EQX0001, and its publish says the native track is experimental in 1.0,
  where a web app's says nothing of it

#### Scenario: A project that uses the native track without its SDK

- **WHEN** a project built with `Microsoft.NET.Sdk` calls a type of `eQuantic.UI.Native.Engine`
- **THEN** the build fails with EQX0001, which names the type and links the wiki section

### Requirement: A stable version removes no member of the contract

When the version in `Directory.Build.props` carries no prerelease label and is not a major's first
release, `x.0.0`, where the major's removals ship, a build of a contract project whose `PublicAPI.Unshipped.txt` holds a `*REMOVED*` line SHALL fail, naming the line and the
two ways on: mark the member obsolete, or move the version to the next major's preview. A tool or an
experimental project, and any project while the version carries a prerelease label, SHALL build.

#### Scenario: A removal at a stable version

- **WHEN** the version is `1.1.0` and a contract project's `PublicAPI.Unshipped.txt` holds a
  `*REMOVED*` line
- **THEN** the build of that project fails, naming the line

#### Scenario: The same removal during a preview

- **WHEN** the version is `2.0.0-preview.1` and the same line is there
- **THEN** the project builds

#### Scenario: A major's first release

- **WHEN** the version is `2.0.0` and the 2.0 preview's `*REMOVED*` lines still wait for the fold
- **THEN** the project builds

#### Scenario: A tool's removal at a stable version

- **WHEN** the version is `1.1.0` and the compiler's `PublicAPI.Unshipped.txt` holds a `*REMOVED*`
  line
- **THEN** the compiler builds

### Requirement: An obsolete member is a promise with a date

While the version carries a prerelease label, no member of the contract assemblies the guard scans
SHALL be marked `[Obsolete]`, since the preview removes it now. Without one, every `[Obsolete]`
member of those assemblies SHALL carry a `DiagnosticId` that starts with `EQO`, a `UrlFormat`, and a
message that names the major version it leaves in, greater than the current major. A second
`Deconstruct` SHALL be refused at any version.

#### Scenario: An obsolete member during the preview

- **WHEN** the version is `0.2.0-preview.63` and a member of `eQuantic.UI.Primitives` is `[Obsolete]`
- **THEN** the guard fails, naming it

#### Scenario: An obsolete member at a stable version

- **WHEN** the version is `1.2.0` and the member carries `DiagnosticId = "EQO0001"`, a `UrlFormat`
  and a message that says it is removed in 2.0
- **THEN** the guard passes

#### Scenario: An obsolete member with no date

- **WHEN** the version is `1.2.0` and the member is `[Obsolete("Use Foo")]`
- **THEN** the guard fails, saying what the attribute lacks

### Requirement: The developer surface keeps its contract lines at a stable version

When the version carries no prerelease label, a line of `developer-surface.baseline.txt` that is not
the native SDK's, the native template's or the `Photon` section's SHALL NOT leave the baseline, and
`EQ_UPDATE_DEVELOPER_SURFACE=1` SHALL refuse to drop it. While the version carries a prerelease label,
the regeneration SHALL write what it finds.

#### Scenario: A property of the web SDK goes at a stable version

- **WHEN** the version is `1.1.0` and an `EQuantic…` property the web SDK reads is removed
- **THEN** `DeveloperSurfaceContractTests` fails, and the regeneration refuses to drop the line

#### Scenario: A property of the native SDK goes at a stable version

- **WHEN** the version is `1.1.0` and a property the native SDK reads is removed
- **THEN** the regeneration drops the line, since the native track is experimental
