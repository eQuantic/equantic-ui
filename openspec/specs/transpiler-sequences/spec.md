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

### Requirement: A class that implements IEnumerable<T> is enumerated as its GetEnumerator() says

A class, a record or a struct that implements `IEnumerable<T>`, or only `IEnumerable`, SHALL be
enumerated in the browser by a `foreach`, a spread, `string.Join` and every LINQ operator as its own
`GetEnumerator()` says, the one C#'s `foreach` binds: its own public one, or else the interface's.
Through the enumerator an iterator method fills, or one the app writes, a class that is its own
enumerator included, it SHALL be walked by its `MoveNext` and `Current` and disposed when the walk
ends, however it ends. A derived type SHALL be enumerated through its override of the method. An
explicit implementation of `GetEnumerator()` or `Current` beside the member that answers that name for
the type SHALL NOT replace it.

#### Scenario: A foreach over an iterator method

- **WHEN** `class Bag : IEnumerable<int>` adds `x * 10` to a list in its `Add`, yields the list in its
  `GetEnumerator()` and implements `IEnumerable.GetEnumerator()` explicitly, and
  `var bag = new Bag(); bag.Add(3); var t = 0; foreach (var x in bag) t += x; return t;`
- **THEN** it answers `30`, as .NET does, where it threw

#### Scenario: string.Join and LINQ

- **WHEN** `var bag = new Bag(); bag.Add(1); bag.Add(2); return string.Join(",", bag);` and
  `new Bag { 1, 2, 3 }.Where(x => x > 10).Sum()`
- **THEN** they answer `10,20` and `50`, as .NET does

#### Scenario: An enumerator the app wrote

- **WHEN** `class Countdown : IEnumerable<int>` returns a `CountdownEnumerator : IEnumerator<int>`
  that counts down and counts its `Dispose` calls, and a `foreach` over `new Countdown(5)` breaks at 4
- **THEN** the loop stops at 4 and the enumerator is disposed once, as .NET disposes it

#### Scenario: A public GetEnumerator that returns a struct

- **WHEN** `class Fast : IEnumerable<int>` declares `public Walk GetEnumerator() => new Walk(3);`, where
  `struct Walk : IEnumerator<int>` answers 2, 4 and 6, beside the explicit `IEnumerable<int>` and
  `IEnumerable` ones, and a `foreach`, `string.Join` and `Sum()` walk it
- **THEN** they answer 12, `2,4,6` and 12, as .NET does, where the explicit one replaced the public one,
  called itself and ran out of stack
