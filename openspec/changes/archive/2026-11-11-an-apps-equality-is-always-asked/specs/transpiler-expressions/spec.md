## ADDED Requirements

### Requirement: An app's Equals is asked every time .NET asks it

An `Equals` the app wrote SHALL run wherever .NET calls it, a value compared with itself included: an instance call through a base or `object`, a member compared inside a record's or a struct's twin, `EqualityComparer<T>.Default`, a collection's search (`List<T>.Contains`, `IndexOf`, `Remove`, an array's, a `HashSet<T>`'s and a `Dictionary<TKey, TValue>`'s key), `Distinct`, and a tuple's, an anonymous object's and a `KeyValuePair`'s member. Only a record's synthesized `Equals` SHALL answer a value compared with itself at its reference check, as .NET's does.

#### Scenario: A value compared with itself

- **WHEN** a program evaluates `object o = c; o.Equals(o)` over a class whose `Equals` counts its calls
- **THEN** the answer is True and the count is 1, as in .NET

#### Scenario: An Equals that refuses itself

- **WHEN** a program evaluates `new List<Refuses> { r }.Contains(r)` over a class whose `Equals` answers false
- **THEN** the answer is False, as in .NET

### Requirement: A call through IEquatable of another type reaches the method it binds

A call through `IEquatable<U>` in generic code SHALL reach the `Equals` the contract binds for the other value's type, where a class implements the interface for several types and never for its own.

#### Scenario: A class that answers several types

- **WHEN** a program evaluates `Same<Multi, int>(m, 1)` and `Same<Multi, string>(m, "a")` through `static bool Same<T, U>(T a, U b) where T : IEquatable<U> => a.Equals(b)`
- **THEN** each answers what the method for its type answers, as in .NET

### Requirement: Two delegates compare by target and method

Two delegates SHALL be equal when they have the same target and the same method, however they were made: `==`, `!=`, `Equals`, `object.Equals`, a collection's search and a dictionary's key SHALL find them equal, and `-=` SHALL remove a handler made from the same method group again. `+` and `+=` of a delegate SHALL combine invocation lists as `Delegate.Combine` does.

#### Scenario: A method group made twice

- **WHEN** a program evaluates `Func<int> m1 = b.M; Func<int> m2 = b.M; m1 == m2`
- **THEN** the answer is True, as in .NET

#### Scenario: A handler removed by its method group

- **WHEN** a program adds `c.OnClick` to an event, removes `c.OnClick` again and raises the event
- **THEN** the handler does not run, as in .NET
