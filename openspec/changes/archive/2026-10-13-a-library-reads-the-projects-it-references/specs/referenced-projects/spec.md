# Spec Delta

## ADDED Requirements

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
- **THEN** the build succeeds and the library's module imports `Ruler`'s

#### Scenario: Projects handed with no graph

- **WHEN** the build is handed the projects and no assets file
- **THEN** it stops with EQ0001, naming `--project-assets`
