# Spec Delta

## ADDED Requirements

### Requirement: An enum member is its name, whatever it is called

An enum member SHALL be written as its enum's representation whatever its name, `Value` and `HasValue`
included. Those two names SHALL be taken for `Nullable<T>`'s members only where the semantic model
cannot say what they are.

#### Scenario: Members named like Nullable's

- **WHEN** a method compares `reading == Reading.Value` and `reading == Reading.HasValue`, where
  `Reading` is an enum
- **THEN** the twin compares with `'value'` and `'hasValue'`, and reads no `Reading` object

#### Scenario: Nullable's own

- **WHEN** a method reads `count.HasValue` and `count.Value` of an `int?`
- **THEN** neither is read as a member in the browser

## MODIFIED Requirements

### Requirement: An annotation names what a module has

A twin's annotation SHALL name a type whose C# name names nothing in TypeScript by what it crosses as:
an exception as `Error`, the JavaScript error that carries its .NET types, an interface as `any`, an
enum as its members (the vocabulary's union, a number when it is [Flags], a string otherwise), and a
delegate as its function. One rule SHALL decide it on every path that writes an annotation: a class's
members and parameters, a record's members, parameters and setters, a local, the item type of an empty
list, and a local function's parameters. A record's static that starts as null SHALL be annotated with
its type and the null. The build context, which C# names `ComponentContext`, SHALL be annotated
`BuildContext`, the name the runtime exports to every module, and a module that writes it SHALL import
it; its `typeScale` SHALL be the number C# holds.

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

#### Scenario: A helper class that takes the build context

- **WHEN** a static class of the shared library declares a method that takes a `ComponentContext` and
  scales a `TypeStyle`'s line height by its `TypeScale`
- **THEN** the twin annotates the parameter `BuildContext`, imports it, and passes the runtime's type
  check
