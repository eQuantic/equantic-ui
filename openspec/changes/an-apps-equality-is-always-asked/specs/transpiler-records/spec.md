## ADDED Requirements

### Requirement: An Equals of the app's is asked every time .NET asks it

A comparison SHALL ask a value's own `Equals` every time .NET's comparer asks it, a value compared with itself included, through an instance call, a member compared inside a twin and a search of a collection. A record's synthesized `Equals` SHALL check the reference first, as C# writes it. A null SHALL equal only a null and SHALL NOT be asked.

#### Scenario: A value compared with itself

- **WHEN** a list holding a value whose `Equals` always answers false is searched for that value
- **THEN** the search finds nothing, as in .NET

### Requirement: A comparison that reaches a server-only Equals is refused

Every comparison the build writes SHALL meet EQ2010 where the equality it reaches is `[ServerOnly]`: a collection's search and construction, LINQ's keys, a tuple's, an anonymous object's and a pair's parts, and an instance call through a contract.

#### Scenario: A set of a type whose Equals stays on the server

- **WHEN** a client component writes `new HashSet<Vaulted> { c }`, where `Vaulted.Equals` is `[ServerOnly]`
- **THEN** the build fails with EQ2010

### Requirement: Two delegates compare by target and method

Two delegates made over one target and method SHALL be equal through `==`, `!=`, `Equals`, a collection's search and a removal with `-=`. A delegate built from another SHALL equal another built from the same source, and not the source. A delegate's `+=`, `-=`, `+` and `-` SHALL combine and remove as `Delegate.Combine` and `Delegate.Remove` do, its target evaluated once.

#### Scenario: A handler made again

- **WHEN** a program writes `button.Clicked += OnClick` and then `button.Clicked -= OnClick`
- **THEN** the event holds no handler

### Requirement: A generic value keeps its closed type where the build knows it

A generic record or struct SHALL carry its closed type when a generic method hands it back to code that names the type arguments, as a struct's zero, and when it is rebuilt from the wire.

#### Scenario: Built in generic code

- **WHEN** `Boxed<T>(T value) => new Box<T>(value)` is called and compared with `new Box<double>(1)`
- **THEN** `Boxed(1).Equals((object)new Box<double>(1))` is false, as in .NET
