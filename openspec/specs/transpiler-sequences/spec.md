# transpiler-sequences Specification

## Purpose
How eqc lets C# read a sequence in the browser as .NET reads it: LINQ over any collection the
browser holds, whatever its shape there, a string as its UTF-16 chars, and the new array LINQ
materializes.

## Requirements

### Requirement: LINQ reads any sequence

A LINQ operator SHALL read its source as the sequence .NET enumerates, whatever the browser holds it
as: an array or a list as it is, a set, a dictionary's pairs and the runtime's sorted set, sorted
dictionary, sorted list, queue, stack and linked list by their own enumeration, and a string by its
UTF-16 code units. A value that is not a sequence SHALL be refused loudly rather than read as empty.

#### Scenario: A queue and a dictionary

- **WHEN** browser-side code computes `q.Where(x => x > 4).Sum() * 10 + q.First()` over
  `new Queue<int>(new[] { 4, 5, 6 })`, and `d.First(kv => kv.Key == "b")` over a dictionary of `a`
  and `b`
- **THEN** the answers are `114` and the pair of `b`, as in .NET

#### Scenario: A stack in its order

- **WHEN** browser-side code computes `t.First() * 10 + t.Last()` over `new Stack<int>(new[] { 1, 2, 3 })`
- **THEN** the answer is `31`, the top first, as in .NET

### Requirement: A string is a sequence of chars

A string read as a sequence, by LINQ, a `foreach` or `ToCharArray`, SHALL give its UTF-16 code units.
`new string(char[])` and `new string(char[], int, int)` SHALL build the text of their chars, a null
array building the empty string, and a char static handed over as a delegate SHALL be the method,
spelled through its type or under `using static System.Char`.

#### Scenario: A surrogate pair

- **WHEN** browser-side code counts the iterations of `foreach (var c in "a😀")`
- **THEN** the count is `3`, as in .NET

#### Scenario: A method group

- **WHEN** browser-side code computes `"a1b2".Count(char.IsDigit)`
- **THEN** the answer is `2`, as in .NET

### Requirement: What LINQ materializes is a copy

`ToList()` and `ToArray()`, LINQ's and a BCL collection's own, SHALL answer a new array of the source's
elements, however the source's static type hides it and whichever operator produced it.

#### Scenario: A list behind a read-only face

- **WHEN** browser-side code sorts `r.ToList()`, where `IReadOnlyCollection<int> r` holds a list
- **THEN** the list keeps its order, as in .NET

### Requirement: A LINQ argument is evaluated once, in C#'s order

The source of a LINQ call and each of its arguments SHALL be evaluated once, in the order C# evaluates them, whatever the lowering does with them per element. A lambda written in place MAY be made again per element, since nothing can tell.

#### Scenario: A second sequence that is a call

- **WHEN** `new[] { 1, 2, 3, 4 }.Intersect(Other())` runs, where `Other` counts its calls
- **THEN** `Other` has run once, as in .NET, and so it has for `Except`

#### Scenario: A source named twice

- **WHEN** `Items().Average()` runs, where `Items` counts its calls
- **THEN** `Items` has run once, as in .NET

#### Scenario: A selector that is a call

- **WHEN** `new[] { 1, 2, 3 }.Sum(Make())` or `GroupBy(Key())` runs, where the factory counts its calls
- **THEN** the factory has run once, as in .NET

#### Scenario: A selector held in a variable

- **WHEN** `new[] { 1, 2, 3 }.Sum(selector)` runs, where `selector` reassigns its own variable on its
  first call
- **THEN** every element goes through the delegate passed, and the sum is 6, as in .NET

#### Scenario: A second sequence with a lowering of its own

- **WHEN** `new[] { 1, 2, 3 }.Intersect(Other().DistinctBy(x => x))` runs, where `Other` counts its calls
- **THEN** `Other` has run once, as in .NET, and so it has for `Except`

#### Scenario: Eleven ordering keys

- **WHEN** an `OrderBy` is followed by ten `ThenBy`
- **THEN** the module parses, and the order is .NET's
