## ADDED Requirements

### Requirement: A null-conditional read answers null where its value is used

A null-conditional read that C# answers with `null` SHALL answer `null` in the browser wherever its
value is used, never `undefined`: assigned, passed, returned, stored or compared. Where nothing can
tell the two apart (a call that returns nothing, a statement that discards the value, the left of a
`??`, the tail of another null-conditional) the translation MAY stay JavaScript's optional chain.

#### Scenario: A dictionary that looks for null

- **WHEN** a value read as `p?.Name` with `p` null is stored in a dictionary, and the code asks
  `ContainsValue(null)`
- **THEN** it answers true, as in .NET

#### Scenario: A chain of two

- **WHEN** `b?.Next?.Tag` is assigned to a local
- **THEN** the local holds null when either link is null, the chain answering once where it ends

#### Scenario: A call that returns nothing

- **WHEN** `b?.Ring()` calls a method that returns `void`
- **THEN** it stays a bare optional chain, with no value to answer

### Requirement: A method group reads its receiver once

A method group bound to its receiver SHALL evaluate the receiver once, when the delegate is made, as C#
does. A receiver whose second read nothing can observe, a plain name or `this`, MAY be written twice.

#### Scenario: A receiver that is a call

- **WHEN** `Func<int> read = c.Make().Value` is made and called, where `Make` counts its calls
- **THEN** `Make` has run once, as in .NET
