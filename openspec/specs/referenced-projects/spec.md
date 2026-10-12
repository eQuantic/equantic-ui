# referenced-projects Specification

## Purpose
Which types of the projects an app references reach the browser, so that a class library shared by
the projects of a solution works in a page as it does on the server: how the build finds those
projects, what it writes for a type the browser code reaches, and what it reports for one that
cannot cross.

## Requirements

### Requirement: A type of a referenced project that browser code reaches is a module of the bundle

When a module the build writes names a class, a record, a struct or a static class declared in a
project the app references, the build SHALL write that type's module and import it, by the rules it
transpiles the app's own types by. It SHALL do the same for every type that module names in turn,
in whichever referenced project it is declared.

#### Scenario: A static member of a referenced project

- **WHEN** a page reads `Palette.Colors`, a `static readonly` member of a static class the
  referenced project `Common` declares
- **THEN** the bundle holds a module `Palette`, the page's module imports it, and the page reads
  the same values in the browser as on the server

#### Scenario: A record of a referenced project built in the browser

- **WHEN** a page's handler runs `new Cursor(3)`, a record the referenced project declares
- **THEN** the bundle holds a module `Cursor` and the value equals another `new Cursor(3)`, as in C#

#### Scenario: A component of a referenced project in a page's tree

- **WHEN** a page's `Build` places `new Wordmark()`, a component the referenced project declares
- **THEN** the server renders it, the bundle holds a module `Wordmark`, and the hydrated page
  rebuilds it in the browser

#### Scenario: A type reached through another referenced project

- **WHEN** a page builds a record of project `Common` whose member is a record of project `Domain`,
  which `Common` references
- **THEN** the bundle holds a module for each

### Requirement: Only what the browser code reaches is compiled

A type of a referenced project that no written module names SHALL NOT be compiled or written, and
SHALL raise no diagnostic, whatever it contains. A type is reached when a written module imports it:
code that does not cross, and a read the build writes as a literal, reach nothing.

#### Scenario: A referenced project that holds server code

- **WHEN** the referenced project also declares a class that reads a file with `System.IO`, and no
  page names it
- **THEN** the build succeeds and the bundle holds no module of that class

#### Scenario: A Server Action names the referenced project's server code

- **WHEN** a page's Server Action builds that class in its body, and nothing else of the page names it
- **THEN** the build succeeds and the bundle holds no module of that class

#### Scenario: A constant read off a referenced project's class

- **WHEN** a static class of the app declares `const string Serif = Theme.Serif;`, where `Theme` is a
  class of the referenced project that also reads a file
- **THEN** the app's module holds the constant's value, imports nothing of `Theme`, and the build
  succeeds with no module of `Theme`

### Requirement: A referenced project's name is imported only where a symbol names its type

A module SHALL import a referenced project's type only where a symbol of the module's own code is
that type. A name bound to another type, or to none, SHALL NOT import a referenced project's module
of that name.

#### Scenario: A package's type of the same name

- **WHEN** a page reads a member of `Status`, a class of a package, and a referenced project declares
  a class named `Status` that nothing reaches
- **THEN** the page's module imports no `Status` and the bundle holds no module of the referenced
  project's

### Requirement: A reached type that cannot cross is reported where it is declared

When a reached type holds what the browser cannot carry, the build SHALL fail with the diagnostic
the same construct raises in the app's own project, at the referenced project's file. A reached
type or member marked `[ServerOnly]` SHALL be refused where the browser code names it.

#### Scenario: A reached class that reads a file

- **WHEN** a page calls a method of a referenced project's class whose body uses `System.IO.File`
- **THEN** the build fails naming that file of the referenced project and the construct

#### Scenario: A record that crossed as data holds a method a browser cannot run

- **WHEN** a Server Action answers a record of a referenced project that declares a method whose
  body uses `System.IO.File`, and no page calls that method
- **THEN** the build fails naming that method's file, where the same record read from an assembly
  crossed as data alone

#### Scenario: A server-only type of a referenced project

- **WHEN** a page names a type the referenced project marks `[ServerOnly]`
- **THEN** the build fails with EQ2010 at the page, and the bundle holds no module of that type

### Requirement: Two reached types of one name are refused

When a reached type of a referenced project has the name of a type the build already writes, the
app's own or another referenced project's, the build SHALL fail naming both declarations (EQ1005).
A type of a referenced project that is not reached SHALL NOT collide with anything.

#### Scenario: The app and the library both declare `Status`, and a page uses the library's

- **WHEN** a page reads a member of the referenced project's `Status` and the app declares a
  `Status` of its own
- **THEN** the build fails with EQ1005 naming the two files

#### Scenario: The app and the library each declare a type of one full name, and both are reached

- **WHEN** the app and the referenced project each declare `internal static class Guard` in the same
  namespace, a page calls the app's, and the page calls a type of the referenced project that calls
  its own
- **THEN** the build fails with EQ1005 at the referenced project's file, and reports no type as
  declared twice (EQ1006)

#### Scenario: The library's `Status` is never used by the browser code

- **WHEN** the app declares `Status`, the referenced project declares another, and no written
  module names the referenced project's
- **THEN** the build succeeds with the app's `Status` as the module

### Requirement: The build finds the referenced projects by itself

The SDK SHALL hand the build every project the app references with a `ProjectReference`, directly
or through another referenced project, with no property or item set by the developer. The SDK's own
projects and icon packs SHALL NOT be among them.

#### Scenario: An app that references a class library

- **WHEN** an app's project file holds `<ProjectReference Include="../Common/Common.csproj" />` and
  nothing else about it
- **THEN** a page that uses a type of `Common` builds and runs in the browser

### Requirement: Each project keeps its own C# scope

A referenced project's source SHALL be read with that project's own `global using` directives and
its own internal types. Neither SHALL reach the app's source or another project's, and a type
declared under one full name in two projects SHALL NOT be an error.

#### Scenario: Two projects each declare an internal `Guard`

- **WHEN** the app and a referenced project each declare `internal static class Guard` in the same
  namespace, and a page uses the app's
- **THEN** the build succeeds and the page's module calls the app's `Guard`

#### Scenario: A referenced project declares the app as a friend

- **WHEN** the referenced project's build declares `InternalsVisibleTo` for the app in a generated
  file, and a page builds an internal record of that project
- **THEN** the build succeeds and the bundle holds that record's module

### Requirement: A value of a referenced project's type crosses the wire as its type

A record or a struct of a referenced project that a Server Action answers, or that a page's state
holds, SHALL be revived in the browser by its own shape, as one the app declares is: a `long`, a
`decimal`, a date and a `Guid` member read as they read in C#.

#### Scenario: A Server Action answers a referenced project's record

- **WHEN** a Server Action answers a record of a referenced project that holds a `long`, a
  `DateTimeOffset` and a `Guid`
- **THEN** the browser reads each member as its C# type, and not as the text the wire carries

### Requirement: A string of a referenced project's catalogue reaches the browser

When a reached type reads a string from a `.resx` of its own project, the client string catalogs
SHALL hold that string for each culture the project has a `.resx` for. A catalogue SHALL be told
apart from every other one of the app and of its referenced projects, whichever file reads it: two
catalogues of one class name SHALL NOT share a key.

#### Scenario: A component of a referenced project reads its project's `Strings.Brand`

- **WHEN** a page places a referenced project's component that reads `Strings.Brand` from that
  project's `Strings.resx`, which has a `Strings.pt-BR.resx` beside it
- **THEN** the neutral catalog and the `pt-BR` one hold that string, and the hydrated page shows it

#### Scenario: The app and the referenced project each have a `Strings`

- **WHEN** a page reads `Title` from the app's own `Strings` and places a referenced project's
  component that reads `Title` from that project's `Strings`, each with a `pt-PT` and a `pt-BR`
  `.resx`, and the two texts differ
- **THEN** the neutral, the `pt-PT` and the `pt-BR` catalogs each hold both strings, and each read
  shows its own project's text in each culture
