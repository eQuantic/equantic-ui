## MODIFIED Requirements

### Requirement: A module imports every type the build writes that it names

A module the build writes SHALL import each type it names that the build writes a module for,
whatever the type is called, under the name its twin takes. A name .NET uses for one of its own types
SHALL be taken for the app's where the module's own code names a type of the app, or of a project it
references, by it, and for .NET's everywhere else.

#### Scenario: A library's class named Type

- **WHEN** a page calls `Type.Family()`, a static class a referenced project declares
- **THEN** the build writes a module `Type` and the page's module imports it

#### Scenario: The app's own class named Type

- **WHEN** a page calls `Type.Family()`, a static class the app declares
- **THEN** the build writes a module `Type` and the page's module imports it

#### Scenario: A library's exception class

- **WHEN** a page builds `new OrderException("x")`, a class a referenced project declares that
  extends `Exception`
- **THEN** the build writes a module `OrderException` and the page's module imports it

#### Scenario: A class named Math beside a file that calls System.Math

- **WHEN** the app or a referenced project declares a class `Math`, one file calls it, and another
  file calls `System.Math`
- **THEN** the first file's module imports `Math$`, and the second imports no module of the class

#### Scenario: One file that calls both

- **WHEN** a page calls `Common.Math.Twice(2) + System.Math.Max(1, 2)`, `Common.Math` being a class of
  the solution
- **THEN** the page's module imports `Math$` and writes `Math$.twice(2) + Math.max(1, 2)`

#### Scenario: An extension declared in a class named Math

- **WHEN** the app or a referenced project declares a class `Math` with an extension method `Twice`,
  and a page calls `2.Twice()`, which the build writes `Math$.twice(2)`
- **THEN** the page's module imports `Math$`, though its source names no `Math`

#### Scenario: A name with no module behind it

- **WHEN** a module names `DateTime` or `Math`, and neither the app nor a referenced project declares
  a type of that name
- **THEN** the module imports no sibling module of that name

## ADDED Requirements

### Requirement: A name inside a string is no reference

A module SHALL import a type only where its text names the type outside a string literal, and a resx
Designer class SHALL be no module, to the project's scan as to the parser.

#### Scenario: A page that reads a string

- **WHEN** a page reads `Strings.Title`, which the build writes `$eq.str('Strings', 'Title')`
- **THEN** the build writes no module `Strings`, and the page's module imports none

#### Scenario: A constant whose value is its class's name

- **WHEN** a page reads `Copy.Title`, a constant of the class `Copy` whose value is `"Copy"`, which the
  build inlines as `'Copy'`
- **THEN** the page's module imports no `./Copy`
