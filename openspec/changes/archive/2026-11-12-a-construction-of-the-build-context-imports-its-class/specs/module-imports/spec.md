## ADDED Requirements

### Requirement: A module imports the name each use of a type is written with

A type the runtime exports under two names SHALL be imported under the name each use of it is written with, as the bound tree says: the build context, `ComponentContext` in C#, under `BuildContext` for an annotation and under `ComponentContext` for a construction, a static member read or called through its name, or a static method called through `using static`, and under both when a module has both.

#### Scenario: A class that annotates and constructs a context

- **WHEN** a shared class keeps a `ComponentContext?` field and returns `_context ??= new ComponentContext(theme)`
- **THEN** its module writes `new ComponentContext(theme)`, imports `BuildContext` and `ComponentContext`, and passes the runtime's `tsc`

#### Scenario: A helper that only takes a context

- **WHEN** a static helper takes a `ComponentContext` parameter and constructs none
- **THEN** its module imports `BuildContext` and not `ComponentContext`
