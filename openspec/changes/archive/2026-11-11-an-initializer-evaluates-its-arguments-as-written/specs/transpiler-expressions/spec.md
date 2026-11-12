## MODIFIED Requirements

### Requirement: A call evaluates its arguments in the order they are written

A call SHALL evaluate its arguments in the order they are written and pass each to the parameter it
binds, wherever it stands (a statement, a concise body, a field's or a property's initializer) and
whichever path places them: an app's method, a vocabulary type's constructor and a twin's. A name an argument written after it changes SHALL be read where it is written.
A variable passed by reference (`ref`, `in`), or an element whose place the call took before it, SHALL be read
when the call is made, after every argument, as the callee reads what it refers to.
An `out` argument SHALL shift none of the arguments after it.

#### Scenario: Named arguments out of order

- **WHEN** `Pair(b: Log(2), a: Log(1))` runs, each argument logging what it returns
- **THEN** the call answers `1,2` and the log reads `21`, as in .NET

#### Scenario: An out argument before a named one

- **WHEN** `Try(out var found, b: 7)` runs, with `a` defaulting to 1
- **THEN** `found` holds what `a = 1, b = 7` make, as in .NET

#### Scenario: A name an argument written after it changes

- **WHEN** `var i = 1; Pair(b: i, a: ++i)` runs
- **THEN** the call answers `2,1`, as in .NET

#### Scenario: A variable passed by reference, which an argument written after it changes

- **WHEN** `var i = 1; RefPair(b: ref i, a: ++i)` runs, `RefPair` reading `a` and `b`
- **THEN** it reads `2,2`, as in .NET, where it read `2,1`

#### Scenario: In a field's initializer

- **WHEN** a class declares `public string Value = Pair(b: Log(2), a: Log(1));` and a property
  `public string Prop { get; } = Pair(b: Log(4), a: Log(3));`, and an instance is built
- **THEN** `Value` reads `1,2`, `Prop` reads `3,4`, and the log reads `2143`, as in .NET
