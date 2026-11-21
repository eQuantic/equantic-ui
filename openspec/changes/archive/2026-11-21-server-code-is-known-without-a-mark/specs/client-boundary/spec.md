## ADDED Requirements

### Requirement: A type of the app no component reaches is server code

The build SHALL write a module for a class, a record, a static class or a generated type of the app's own
only when it is reached from the app's components (its pages and its layout among them) through the
imports of sibling modules each written module holds, as a referenced project's type is. It SHALL say
nothing a type never reached reported, and SHALL write no module for it. A component SHALL be a module
wherever its file sits.

#### Scenario: A class that reads a file, named by no component

- **WHEN** an app declares `public static class Disk { public static bool Has(string path) => File.Exists(path); }` and no component names it
- **THEN** the build succeeds, says nothing, and writes no module `Disk`

#### Scenario: The same class, named by a page

- **WHEN** a page shows `App.Model.Disk.Has("x") ? "kept" : "gone"`
- **THEN** the build fails with EQ2112 at `Disk`'s file

#### Scenario: A middleware and a generated regex

- **WHEN** the dashboard sample holds a middleware that reads `HttpContext` and `System.IO.Path`, with no
  `[ServerOnly]`, and a `[ServerOnly]` static class with a `[GeneratedRegex]`, and no component names either
- **THEN** the sample builds with no error, and its served JavaScript is what it was without them
