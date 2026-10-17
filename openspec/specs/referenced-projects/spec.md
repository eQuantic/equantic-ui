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
code that does not cross, and a name the build writes nothing for (inside `nameof`, in an attribute,
or a constant written as its value), reach nothing.

#### Scenario: A referenced project that holds server code

- **WHEN** the referenced project also declares a class that reads a file with `System.IO`, and no
  page names it
- **THEN** the build succeeds and the bundle holds no module of that class

#### Scenario: A Server Action names the referenced project's server code

- **WHEN** a page's Server Action builds that class in its body, and nothing else of the page names it
- **THEN** the build succeeds and the bundle holds no module of that class

#### Scenario: A Server Action maps a referenced project's type of the app's name

- **WHEN** a page shows the app's own `Order`, and its Server Action, or a `[ServerOnly]` method of
  it, builds that `Order` from the referenced project's `Order`, a class that reads a file
- **THEN** the build succeeds with the app's `Order` as the module, and the referenced project's is
  not compiled

#### Scenario: A Server Action's answer is named by its return type alone

- **WHEN** a Server Action answers a record of the referenced project, and nothing else of the page
  names that record
- **THEN** the page's module imports the record, and the bundle holds its module

#### Scenario: A constant read off a referenced project's class

- **WHEN** a static class of the app declares `const string Serif = Theme.Serif;`, where `Theme` is a
  class of the referenced project that also reads a file
- **THEN** the app's module holds the constant's value, imports nothing of `Theme`, and the build
  succeeds with no module of `Theme`

#### Scenario: A name written nowhere names a referenced project's type of the app's name

- **WHEN** a page shows the app's own `Order`, and names the referenced project's `Order`, a class
  that reads a file, only inside `nameof`, in an attribute, or by reading a constant of it
- **THEN** the build succeeds with the app's `Order` as the module, the name and the constant are
  written as text, and the referenced project's `Order` is not compiled

#### Scenario: A constant written as a reference

- **WHEN** a page reads a constant of a referenced project's static class by its bare name, under
  `using static`
- **THEN** the page's module reads it off that class's twin and imports the class, and the bundle
  holds its module

### Requirement: A referenced project's name is imported only where a symbol names its type

A module SHALL import a referenced project's type only where a symbol of the module's own code is
that type. A name bound to another type, or to none, SHALL NOT import a referenced project's module
of that name.

#### Scenario: A package's type of the same name

- **WHEN** a page reads `Status.Label`, a static of a class of a package, and a referenced project
  declares a class named `Status` that nothing reaches
- **THEN** the build fails with EQ2004 naming the package's `Vendor.Status.Label`, never the referenced
  project's, and the bundle holds no module of the referenced project's

### Requirement: A reached type that cannot cross is reported where it is declared

When a reached type holds what the browser cannot carry, the build SHALL fail with the diagnostic
the same construct raises in the app's own project, at the referenced project's file. A reached
type or member marked `[ServerOnly]` SHALL be refused where the browser code names it. A member
marked so SHALL be refused in the code of its own type too, in a referenced project as in the app:
a component's twin has no such member to call.

#### Scenario: A reached class that reads a file

- **WHEN** a page calls a method of a referenced project's class whose body uses `System.IO.File`
- **THEN** the build fails naming that file of the referenced project and the construct

#### Scenario: A record that crossed as data holds a method a browser cannot run

- **WHEN** a Server Action answers a record of a referenced project that declares a method whose
  body uses `System.IO.File`, and no page calls that method
- **THEN** the build fails naming that method's file, where the same record read from an assembly
  crossed as data alone

#### Scenario: A component calls its own server-only method

- **WHEN** a component, the app's or a referenced project's, calls a `[ServerOnly]` method of its
  own from its `Build`
- **THEN** the build fails with EQ2010 at that call, in the component's file

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

#### Scenario: One file of the library declares two types of one name, and a page reaches one

- **WHEN** a file of the referenced project declares a `Status` in each of two namespaces, the
  second of which reads a file, and a page builds the first
- **THEN** the build succeeds with the first as the module, and the second is not compiled

#### Scenario: The library's `Status` is never used by the browser code

- **WHEN** the app declares `Status`, the referenced project declares another, and no written
  module names the referenced project's
- **THEN** the build succeeds with the app's `Status` as the module

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

### Requirement: Each project keeps its own C# scope

A referenced project's source SHALL be read with that project's own `global using` directives and
its own internal types. Neither SHALL reach the app's source or another project's, and a type
declared under one full name in two projects SHALL NOT be an error. The app SHALL be known to the
build by the assembly name its project evaluates to, which is the name a friend declaration names.

#### Scenario: Two projects each declare an internal `Guard`

- **WHEN** the app and a referenced project each declare `internal static class Guard` in the same
  namespace, and a page uses the app's
- **THEN** the build succeeds and the page's module calls the app's `Guard`

#### Scenario: A referenced project declares the app as a friend

- **WHEN** the referenced project's build declares `InternalsVisibleTo` for the app in a generated
  file, and a page builds an internal record of that project
- **THEN** the build succeeds and the bundle holds that record's module

#### Scenario: An app whose assembly is named apart from its project file

- **WHEN** an app's project file sets `<AssemblyName>` to a name that is not the file's, the
  referenced project declares `InternalsVisibleTo` for that name, and a page builds an internal
  record of that project
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

### Requirement: What the SDK hands the build reaches it as it is

The paths and the name the SDK hands the build SHALL reach it as they are, whatever characters they
hold that a file name or an assembly name may hold: a dollar sign, a backtick, a bar, a space, a
quote. No shell SHALL read them on the way, and each program of the build SHALL receive a path as an
argument of its own.

#### Scenario: The list of projects under a directory whose name holds a bar

- **WHEN** the projects the build is handed sit under a directory named `a|b $c`, on a system whose
  paths may hold a bar
- **THEN** each project is read whole, and a type of the library that a page uses is a module of the
  bundle

#### Scenario: An app under a directory whose name holds a dollar sign and quotes

- **WHEN** an app and the class library it references sit under a directory named `app $b 'c'`, and
  the app is built from packages alone
- **THEN** the build succeeds, and the bundle holds the library's types the page uses

#### Scenario: An app under a directory whose name holds a double quote

- **WHEN** an app and the class library it references sit under a directory named `app "e"`, on a
  system whose paths may hold a double quote, and the app is built from packages alone
- **THEN** the build succeeds, and the page runs as it does under any other directory

#### Scenario: An app whose assembly name holds a dollar sign

- **WHEN** an app's project file sets `<AssemblyName>Cash$Flow</AssemblyName>`
- **THEN** the build is handed `Cash$Flow`, and a library's `InternalsVisibleTo("Cash$Flow")` opens
  its internal types to the app

### Requirement: A library's code reads the projects it references

The code of each project the app references SHALL bind against the projects that project references,
as the C# compiler binds it, whatever it reads of them. The build SHALL take which project references
which from the graph NuGet's restore wrote for the app, the app's assets file, and never from the
projects' assemblies, which name no reference that only a constant or a method body makes.

#### Scenario: A library reads a constant of a project it references

- **WHEN** a library the app references declares `private const string Serif = Fonts.Serif;`, where
  `Fonts` is a class of a project the library references and reads nothing else of
- **THEN** the build succeeds, and a page that shows the library's value shows `Newsreader` in the
  browser

#### Scenario: A library uses a type of a project it references only inside a method

- **WHEN** a method body of the library builds `new Ruler()`, a class of that project, and no
  signature of the library names it
- **THEN** the build succeeds and the library's module imports `Ruler`'s module

#### Scenario: A project reached through one with no source of its own

- **WHEN** the library references a project that holds no C# of its own, which references the
  project the library's code reads
- **THEN** the build succeeds, as references flow through the project in between

#### Scenario: Projects handed with no graph

- **WHEN** the build is handed the projects and no assets file
- **THEN** it stops with EQ0001, naming `--project-assets`
