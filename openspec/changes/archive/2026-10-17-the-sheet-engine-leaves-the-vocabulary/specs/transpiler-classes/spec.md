## MODIFIED Requirements

### Requirement: A transpiled vocabulary type is built as an app's type is

A type the runtime provides and transpiles from its C# SHALL be built by an app as the app's own types
are: the constructor the call binds, then the object initializer applied to what it built. Those are
the types of the two engines' assemblies, `eQuantic.UI.Code` and `eQuantic.UI.Sheet`, whose namespaces
say so, and the types of the vocabulary's own assembly marked `[TwinIsTranspiled]`. Its zero, its
indexer and its type test SHALL be those of a twin eqc writes.

#### Scenario: A record struct and a record built with an initializer

- **WHEN** `new CellRef(1, 2) { Col = 3 }` and `new FieldError("name", "required") { Message = "too short" }`
- **THEN** the cell is `1|3` and the error's message is `too short`, as in .NET

#### Scenario: A class built with an initializer

- **WHEN** `new SheetController(10, 4) { Changed = edit => { } }` and `new SheetEdit { At = 3, Count = 2 }`
- **THEN** the controller holds its handler and the edit is `3|2`, as in .NET

#### Scenario: A record struct's zero

- **WHEN** `CellRef c = default;`
- **THEN** `c` is `0|0`, as in .NET
