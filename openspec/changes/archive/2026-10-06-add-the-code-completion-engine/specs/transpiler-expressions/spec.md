# Spec Delta

## ADDED Requirements

### Requirement: A method named Invoke is a method

`x.Invoke(…)` SHALL be a call of `x` only when `x` is a delegate, decided by the bound symbol; a method
of that name on any other type SHALL be called as the method it is.

#### Scenario: A class with an Invoke method

- **WHEN** a class declares `int Invoke()` and another calls `other.Invoke()`
- **THEN** the twin calls `other.invoke()`, and a `Func<int>`'s `make.Invoke()` is `make()`

### Requirement: An annotation names what a module has

A twin's annotation SHALL name a type whose C# name names nothing in TypeScript by what it crosses as:
an exception as `Error`, the JavaScript error that carries its .NET types, an interface as `any`, an
enum as its members (the vocabulary's union, a number when it is [Flags], a string otherwise), and a
delegate as its function. One rule SHALL decide it on every path that writes an annotation: a class's
members and parameters, a record's members, parameters and setters, a local, the item type of an empty
list, and a local function's parameters. A record's static that starts as null SHALL be annotated with
its type and the null.

#### Scenario: An event of exceptions and lists of an interface and of exceptions

- **WHEN** a class declares `event Action<Exception>? Failed` and a method declares
  `new List<Exception>()` and `new List<IDisposable>()`
- **THEN** the twin annotates `(exception: Error) => void`, `Error[]` and `any[]`

#### Scenario: A record and a method that reach every path

- **WHEN** a record declares an `Exception`, an interface, an app enum and a delegate `Measure` among
  its members, and a method declares an `IThing` local, a null `Exception?`, an empty `List<Action>`
  and a local function taking an exception
- **THEN** no annotation of the twin names `Exception`, `IThing`, the enum, `Measure` or `Action`, and
  they read `Error`, `any`, `string`, `(text: string) => number` and `(() => void)[]`
