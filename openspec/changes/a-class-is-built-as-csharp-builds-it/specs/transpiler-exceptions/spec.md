## ADDED Requirements

### Requirement: An exception's object initializer is applied once it is built

`new T(…) { … }` for an exception type SHALL build the exception with T's chain, as any construction
of T does, and SHALL then apply the object initializer to it, in the order it is written, as a
record's is applied: an assignment to a field or a property, and an `Add` per element of a nested
collection initializer.

#### Scenario: A field set by an initializer

- **WHEN** `class Failure : Exception { }`, `class Retry : Failure { public int Attempts; }` and
  `new Retry { Attempts = 3 }`
- **THEN** its `Attempts` is `3`, and it is a `Failure`, as in .NET

#### Scenario: A property and a nested collection

- **WHEN** `class Batch : Exception { public string Name { get; set; } = "none"; public List<int> Ids { get; } = new(); }`
  and `new Batch { Name = "b", Ids = { 1, 2 } }`
- **THEN** its `Name` is `b` and its `Ids` holds `1` and `2`, as in .NET
