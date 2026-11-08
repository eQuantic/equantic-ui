## ADDED Requirements

### Requirement: A call evaluates its arguments in the order they are written

A call SHALL evaluate its arguments in the order they are written and pass each to the parameter it
binds, whichever path places them: an app's method, a vocabulary type's constructor and a twin's. An
`out` argument SHALL shift none of the arguments after it.

#### Scenario: Named arguments out of order

- **WHEN** `Pair(b: Log(2), a: Log(1))` runs, each argument logging what it returns
- **THEN** the call answers `1,2` and the log reads `21`, as in .NET

#### Scenario: An out argument before a named one

- **WHEN** `Try(out var found, b: 7)` runs, with `a` defaulting to 1
- **THEN** `found` holds what `a = 1, b = 7` make, as in .NET
