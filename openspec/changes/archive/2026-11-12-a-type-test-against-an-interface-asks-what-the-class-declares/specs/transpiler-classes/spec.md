## ADDED Requirements

### Requirement: A type test against an interface asks what the value's class declares

A type test against an interface (`is`, `as`, a type pattern, a switch arm) SHALL answer what .NET answers off the type: whether the value's class implements the interface, its bases included, whatever members the value has. The twin of a class, a record, a struct and a component SHALL declare the interfaces a test can ask for, which are an interface of the app's own or of a project it references, one of an assembly the runtime carries transpiled whole, and `IDisposable`. A test against any other interface SHALL be refused with EQ2015.

#### Scenario: A class that implements the interface and one that does not

- **WHEN** `object o = new Square(); return o is IShape;` and `object o = new Plain(); return o is IShape;` run, where `Square` implements `IShape` and `Plain` only has its member
- **THEN** they answer true and false, as .NET does

#### Scenario: Through a base, a record and a boxed struct

- **WHEN** a type pattern `IShape s` meets a class derived from one that implements `IShape`, a positional record that implements it, and a boxed struct that implements it
- **THEN** each binds, and its `Sides` is what .NET reads

#### Scenario: A Dispose that is not IDisposable's

- **WHEN** `(o as IDisposable)?.Dispose()` runs over a class that implements `IDisposable` and over one that only has a `Dispose` method
- **THEN** the first is disposed and the second is not, as in .NET

#### Scenario: A test the browser cannot answer

- **WHEN** a program tests `o is IComparable`, `o is IEnumerable`, `o is IEnumerable<int>` or `o is IBox<int>`
- **THEN** the build refuses it with EQ2015
