# Spec Delta

## ADDED Requirements

### Requirement: A value written for a constant is converted where C# converts it

A constant of a referenced project that is written as its value, and `nameof`, SHALL still be
converted where C# applies a user-defined conversion to it, and the module that writes the
conversion SHALL import the type that declares it.

#### Scenario: A constant handed to an indexer that takes a converted type

- **WHEN** a referenced project declares `record struct Slot(int Index)` with an implicit conversion
  from `int`, a class whose indexer takes a `Slot`, and `const int First = 1`, and a page reads
  `board[Limits.First]`
- **THEN** the page's module writes the conversion around the value and imports `Slot`

#### Scenario: nameof handed to an indexer that takes a converted type

- **WHEN** the indexer takes a `Tag` with an implicit conversion from `string`, and a page reads
  `shelf[nameof(Home)]`
- **THEN** the page's module writes the conversion around the name and imports `Tag`

## MODIFIED Requirements

### Requirement: The build finds the referenced projects by itself

The SDK SHALL hand the build every project the app references with a `ProjectReference`, directly
or through another referenced project, with no property or item set by the developer. The SDK's own
projects and icon packs SHALL NOT be among them. A list that was handed and does not hold SHALL stop
the build with a diagnostic, and SHALL never be read as fewer projects, a list that holds no project
included.

#### Scenario: An app that references a class library

- **WHEN** an app's project file holds `<ProjectReference Include="../Common/Common.csproj" />` and
  nothing else about it
- **THEN** a page that uses a type of `Common` builds and runs in the browser

#### Scenario: The list the SDK handed does not hold

- **WHEN** the file the build was handed is not there, holds no project, has a line that is not a
  project, or a line names an assembly or a project that is not there
- **THEN** the build fails with EQ0001 naming the list and what did not hold, and writes no module
