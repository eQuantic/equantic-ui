# runtime-collections Specification

## Purpose
What the runtime's `Queue`, `Stack`, `LinkedList` and `SortedSet` answer on the web, starting with
the JSON each writes when it crosses the wire.

## Requirements

### Requirement: A collection writes the JSON .NET writes

The runtime's `Queue`, `Stack`, `LinkedList` and `SortedSet` SHALL write themselves to JSON as
System.Text.Json writes them: an array of their elements in the order each enumerates, a stack from
its top.

#### Scenario: A queue

- **WHEN** `new Queue<long>(new[] { 1L, 2L })` is written to JSON
- **THEN** it is `["1","2"]` on both sides, a long as a BigInt's digits, where the browser wrote `{"items":[...]}`

#### Scenario: A stack

- **WHEN** `new Stack<int>(new[] { 1, 2, 3 })` is written to JSON
- **THEN** it is `[3,2,1]`, as in .NET

### Requirement: A queue, a stack and a sorted set copy and combine as .NET's do

The runtime's `Queue` and `Stack` SHALL answer `CopyTo`, and its `SortedSet` SHALL answer `CopyTo`,
`UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith`, `IsSubsetOf`, `IsProperSubsetOf`,
`IsSupersetOf`, `IsProperSupersetOf`, `Overlaps`, `SetEquals` and `RemoveWhere`, each as .NET answers
it: the same elements in the same order, the same refusals in the same words, and an argument that
repeats an element, the empty set and the set itself handled as .NET handles them. Of two elements the
set's comparer finds equal, a union and an intersection SHALL keep the instance .NET keeps.

#### Scenario: A queue's copy

- **WHEN** browser-side code dequeues 1 from `new Queue<int>(new[] { 1, 2, 3 })`, enqueues 4, and copies it into `new int[5]` from index 1
- **THEN** the array is `0,2,3,4,0`, as in .NET

#### Scenario: A stack's copy

- **WHEN** browser-side code pushes 1, 2 and 3 onto a stack and copies it into `new int[4]` from index 1
- **THEN** the array is `0,3,2,1`, as in .NET

#### Scenario: A copy refused

- **WHEN** browser-side code copies a sorted set of three elements into `new int[4]` from index 2
- **THEN** it throws the `ArgumentException` .NET throws, "Destination array is not long enough to copy all the items in the collection. Check array index and length."

#### Scenario: A symmetric difference over repeated elements

- **WHEN** browser-side code computes `SymmetricExceptWith(new[] { 4, 2, 4, 2, 5 })` over `new SortedSet<int> { 1, 2, 3 }`
- **THEN** the set is `1,3,4,5`, as in .NET

#### Scenario: The instance a merge keeps

- **WHEN** browser-side code unions `new SortedSet<decimal> { 1.0m, 2m }` with `new SortedSet<decimal> { 1.00m, 3m }`
- **THEN** the set prints `1.00,2,3`, as in .NET

#### Scenario: The instance an addition keeps

- **WHEN** browser-side code unions `new SortedSet<decimal> { 1.0m, 2m }` with `new List<decimal> { 1.00m, 3m }`
- **THEN** the set prints `1.0,2,3`, as in .NET

#### Scenario: A question over repeated elements

- **WHEN** browser-side code asks `new SortedSet<int> { 1, 2 }` whether it `IsProperSubsetOf(new[] { 2, 1, 1 })` and whether it `SetEquals(new[] { 2, 1, 2 })`
- **THEN** the answers are False and True, as in .NET

#### Scenario: A null argument

- **WHEN** browser-side code calls `UnionWith(null)` on an empty sorted set
- **THEN** it throws the `ArgumentNullException` .NET throws, naming `other`

### Requirement: A collection's constructor refuses a null source by its parameter's name

A construction of a `List`, `Queue`, `Stack`, `LinkedList`, `SortedSet`, `Dictionary`,
`SortedDictionary` or `SortedList` that copies a source SHALL refuse a null one with .NET's
`ArgumentNullException`, naming the parameter the source binds to (`collection` or `dictionary`), and a
`Queue` or a `Stack` SHALL refuse a negative capacity in .NET's words. A string copied into a
collection SHALL be read by its UTF-16 chars.

#### Scenario: A null collection

- **WHEN** browser-side code builds `new Queue<int>(source)` where `source` is a null `IEnumerable<int>`
- **THEN** it throws the `ArgumentNullException` .NET throws, "Value cannot be null. (Parameter 'collection')"

#### Scenario: A null dictionary

- **WHEN** browser-side code builds `new Dictionary<string, int>(source, StringComparer.Ordinal)` where `source` is a null `IDictionary<string, int>`
- **THEN** it throws the `ArgumentNullException` .NET throws, naming `dictionary`

#### Scenario: A null list source

- **WHEN** browser-side code builds `new List<int>(source)` where `source` is a null `IEnumerable<int>`
- **THEN** it throws the `ArgumentNullException` .NET throws, naming `collection`, where a spread threw a `TypeError`

#### Scenario: A string's chars

- **WHEN** browser-side code counts `new List<char>("a\U0001F600b")`
- **THEN** the answer is 4, as in .NET
