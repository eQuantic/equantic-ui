## ADDED Requirements

### Requirement: A generic value keeps its closed type wherever it is made

A generic record or struct SHALL carry its closed type where generic code builds it, a generic method's and a generic class's member included, where it is a struct's zero (a `default`, a field's and an array's element), and where it is rebuilt from the wire, so it is never equal to a value of another type argument.

#### Scenario: Built in generic code

- **WHEN** a program evaluates `Make.Boxed(1).Equals((object)new Box<double>(1))`, where `Boxed<T>(T value) => new Box<T>(value)`
- **THEN** the answer is False, as in .NET

#### Scenario: A struct's zero

- **WHEN** a program evaluates `default(Pair<int>).Equals((object)new Pair<double>(0))` and `default(Pair<int>).Equals((object)new Pair<int>(0))`
- **THEN** the answers are False and True, as in .NET
