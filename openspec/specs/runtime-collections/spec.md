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
