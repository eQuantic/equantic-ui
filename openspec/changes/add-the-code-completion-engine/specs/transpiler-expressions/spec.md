# Spec Delta

## ADDED Requirements

### Requirement: A method named Invoke is a method

`x.Invoke(…)` SHALL be a call of `x` only when `x` is a delegate, decided by the bound symbol; a method
of that name on any other type SHALL be called as the method it is.

#### Scenario: A class with an Invoke method

- **WHEN** a class declares `int Invoke()` and another calls `other.Invoke()`
- **THEN** the twin calls `other.invoke()`, and a `Func<int>`'s `make.Invoke()` is `make()`

### Requirement: An annotation names what a module has

A twin's annotation SHALL name an exception as `Error`, the JavaScript error that carries its .NET
types, and an interface as `any`, in a delegate's parameters, in a parameter, and in the item type of
an empty list.

#### Scenario: An event of exceptions and lists of an interface and of exceptions

- **WHEN** a class declares `event Action<Exception>? Failed` and a method declares
  `new List<Exception>()` and `new List<IDisposable>()`
- **THEN** the twin annotates `(exception: Error) => void`, `Error[]` and `any[]`
