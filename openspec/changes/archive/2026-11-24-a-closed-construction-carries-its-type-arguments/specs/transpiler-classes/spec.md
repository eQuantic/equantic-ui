## ADDED Requirements

### Requirement: A runtime library's generic, built by an app, keeps its type arguments

A closed construction of a generic class, record or struct the runtime library twins SHALL be marked with its
type arguments wherever an app writes it, as one the app's source declares is, though the app reads that
generic from metadata. A generic the runtime maps to JavaScript's own types, and a construction whose type
arguments are known only at run time, SHALL stay unmarked.

#### Scenario: A factory of the framework's over an app's type

- **WHEN** an app builds `new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(…)`, the factory read from
  the framework's assembly
- **THEN** the value is marked with `HoldThenDrag`, so its runtime type is not that of a factory over another
  type, as in .NET

#### Scenario: A collection of the BCL's

- **WHEN** an app builds `new List<int>()`
- **THEN** it crosses as before, a JavaScript array with no mark
