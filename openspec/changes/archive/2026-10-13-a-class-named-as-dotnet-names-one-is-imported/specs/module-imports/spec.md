# Spec Delta

## ADDED Requirements

### Requirement: A module imports every type the build writes that it names

A module the build writes SHALL import each type it names that the build writes a module for,
whatever the type is called. A name .NET uses for one of its own types SHALL be taken for .NET's only
where the build writes no module of that name.

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

#### Scenario: A name with no module behind it

- **WHEN** a module names `DateTime` or `Math`, and neither the app nor a referenced project declares
  a type of that name
- **THEN** the module imports no sibling module of that name
