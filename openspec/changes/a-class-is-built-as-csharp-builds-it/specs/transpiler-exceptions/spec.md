## ADDED Requirements

### Requirement: An exception's object initializer is applied once it is built

`new T(…) { … }` for an exception type SHALL build the exception with T's chain, as any construction
of T does, and SHALL then apply the object initializer to it as a record's is applied: each member
it names is assigned, in the order it is written, after the exception is built.

#### Scenario: A field set by an initializer

- **WHEN** `class Failure : Exception { }`, `class Retry : Failure { public int Attempts; }` and
  `new Retry { Attempts = 3 }`
- **THEN** its `Attempts` is `3`, and it is a `Failure`, as in .NET

#### Scenario: Two members, each evaluated after the exception is built

- **WHEN** `class Coded : Exception { public int Code; public string Hint { get; set; } }` and
  `new Coded { Code = 4, Hint = "retry" }`
- **THEN** its `Code` is `4` and its `Hint` is `retry`, as in .NET
