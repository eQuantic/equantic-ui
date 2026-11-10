## ADDED Requirements

### Requirement: An array behind a collection's face keeps its size

A `T[]` converted to `ICollection<T>` or `IList<T>` SHALL refuse `Add`, `Remove`, `Insert` and `RemoveAt` with `NotSupportedException` "Collection was of a fixed size.", and `Clear` with "Collection is read-only.", and `IsReadOnly` SHALL answer true. Writing an element SHALL stay allowed, and a `List<T>` behind the same faces SHALL keep growing.

#### Scenario: Adding through the interface

- **WHEN** a program evaluates `((ICollection<int>)new[] { 1, 2 }).Add(3)`
- **THEN** the call throws `NotSupportedException`, and the array keeps its two elements

### Requirement: A params parameter is the array it is

A `params` parameter of a method, a local function or a delegate on a twin the build writes SHALL be one array: a call that lists its elements packs them, none being an empty array, and an array handed whole SHALL be passed as it is, the callee holding the caller's array, with no limit on its length.

#### Scenario: A large array

- **WHEN** a program calls `Count(items)` over `int Count(params int[] xs)` with an array of a million elements
- **THEN** the call answers 1000000, as in .NET

#### Scenario: The caller's array

- **WHEN** a callee writes `xs[0] = 0` over an array handed whole
- **THEN** the caller's array holds the 0, as in .NET
