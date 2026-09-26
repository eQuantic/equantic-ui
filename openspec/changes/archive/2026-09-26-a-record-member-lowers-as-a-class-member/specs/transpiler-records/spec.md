# Spec Delta

## ADDED Requirements

### Requirement: A record's and a struct's members lower as a class's do

A record's and a struct's method, operator, conversion and computed property SHALL be lowered
through the same lowering as a class's members, their own and a default an interface supplies: an
async method SHALL run as an async function, an iterator SHALL return its sequence, an `out` or
`ref` parameter SHALL carry its value back, and a variable an expression body's pattern binds SHALL
be declared before its use. A parameter SHALL be declared by the name every reference to it uses.
Whether a method is async SHALL be decided by its `async` modifier or its return type's symbol, never
by the type's name, and a getter that yields SHALL return its sequence.

#### Scenario: An async method and an iterator

- **WHEN** `record R(int V)` declares `async Task<int> Own() => await Task.FromResult(V);` and
  `IEnumerable<int> Mine() { yield return V; yield return V + 1; }`
- **THEN** `await new R(3).Own()` answers 3 and `string.Join(",", new R(3).Mine())` answers `3,4`, as in .NET

#### Scenario: A pattern variable in an expression body

- **WHEN** `struct SE` declares `public override bool Equals(object o) => o is SE m && m.V == V;`
- **THEN** `new SE(1).Equals(new SE(1))` answers true, and `List<SE>.Remove(new SE(1))` removes it

#### Scenario: A return type named like a task

- **WHEN** `record R(int V)` declares `TaskItem First() => new TaskItem(V);` with `record TaskItem(int N)`
- **THEN** `new R(4).First().N` answers 4, the method not being async

#### Scenario: A reserved parameter name

- **WHEN** a record's method takes `int package` or `int @class`
- **THEN** it runs, the parameter declared as `package_` or `class_`, the name every use of it has
