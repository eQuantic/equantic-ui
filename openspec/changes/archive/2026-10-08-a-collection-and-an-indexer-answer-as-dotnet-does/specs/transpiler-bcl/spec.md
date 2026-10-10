## ADDED Requirements

### Requirement: A list built with an argument and an initializer holds both

A `List<T>` constructed with an argument and a collection initializer SHALL hold what .NET's holds:
a copy of the source, never the source itself, or nothing for a capacity, then each element of the
initializer in order, in the explicit and the target-typed forms alike, the source evaluated before the
elements. A target-typed list built from a source with no initializer SHALL be a copy of the source.

#### Scenario: A capacity and an initializer

- **WHEN** browser-side code builds `new List<int>(10) { 1, 2 }` and joins it
- **THEN** the answer is `1,2|2` with its count, as in .NET

#### Scenario: A source and an initializer

- **WHEN** browser-side code builds `var b = new List<int>(source) { 3 }` over `source = new List<int> { 1 }`, then runs `b.Add(4)`
- **THEN** `b` holds `1,3,4` and `source` still holds `1`, as in .NET

#### Scenario: A target-typed copy

- **WHEN** browser-side code builds `List<int> c = new(source)` over `source = new List<int> { 1 }` and runs `c.Add(9)`
- **THEN** `c` holds `1,9` and `source` one element, as in .NET

#### Scenario: A capacity is evaluated first

- **WHEN** browser-side code builds `new List<int>(Cap()) { At("a", 1), At("b", 2) }`, each call logging its step, and `new List<int>(-1)`
- **THEN** the log is `cab` before the list holds `1,2`, and the second throws an ArgumentOutOfRangeException, "Non-negative number required. (Parameter 'capacity')", as in .NET

### Requirement: A list's face reads and writes the list behind it

An element read or written through the indexer of `IList<T>`, `IReadOnlyList<T>` or `IList`, and a
`Count` read through such a face, SHALL answer for whatever the face holds when it runs: an array's or
a list's elements, and a twin's own indexer and count. A read, a write, a compound, a step, a
null-conditional read, an object initializer's entry and a key from the end SHALL each reach it, the
receiver and the key evaluated once and in C#'s order, and a write SHALL answer the value it wrote.

#### Scenario: A twin behind IReadOnlyList

- **WHEN** browser-side code computes `r[0] + r[2] + r.Count` over `IReadOnlyList<int> r` holding a twin of `{ 7, 8, 9 }`
- **THEN** the answer is `19`, as in .NET

#### Scenario: A twin behind IList

- **WHEN** browser-side code runs `l[0] = 5; l[1] += 2; l[2]++;` over `IList<int> l` holding a twin of `{ 1, 2, 3 }`
- **THEN** `l[0]`, `l[1]` and `l[2]` are `5`, `4` and `4`, as in .NET

#### Scenario: A twin with a Length beside its Count

- **WHEN** browser-side code reads `p.Count` and `p[^1]` over `IReadOnlyList<int> p` holding a twin of `{ 3, 4 }` that also declares `Length => 12.5` and `Size => 7`
- **THEN** the answers are `2` and `4`, as in .NET

#### Scenario: A face that holds null

- **WHEN** browser-side code reads `r.Count`, `r[0]` and `r?.Count` over `IReadOnlyList<int> r = null`
- **THEN** the first two throw a NullReferenceException, "Object reference not set to an instance of an object.", and the null-conditional answers null, as in .NET

### Requirement: ICollection's Add and Clear answer for the collection behind it

`ICollection<T>.Add` and `ICollection<T>.Clear` SHALL add and empty as the collection the interface
holds when the call runs does in .NET: a list appends, a set adds only a value it does not hold, a
linked list adds last, a dictionary adds the pair and refuses a key already there with .NET's message,
and a twin of the app's own runs its own `Add` and `Clear`. A collection initializer that adds to a
member typed as the interface SHALL add the same way.

#### Scenario: A set behind ICollection

- **WHEN** browser-side code runs `c.Add(2); c.Add(1);` over `ICollection<int> c = new HashSet<int> { 1 }` and reads `c.Count`
- **THEN** the count is `2`, as in .NET, where the call threw `c.push is not a function`

#### Scenario: A dictionary's pairs behind ICollection

- **WHEN** browser-side code adds the pair `("a", 2)` to `ICollection<KeyValuePair<string, int>>` holding a dictionary with the key `a`
- **THEN** it throws `An item with the same key has already been added. Key: a`, as in .NET
