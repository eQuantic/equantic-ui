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

#### Scenario: A source a selector reassigns

- **WHEN** `xs.Average(x => { xs = new[] { 1 }; return x; })` runs over `{ 1, 2, 3, 4 }`
- **THEN** it answers 2.5, as in .NET, which read `xs` once

#### Scenario: A string that quotes a lowering's own name

- **WHEN** `new[] { 1, 2, 3, 4 }.Intersect(Other("$x"))` runs, where `Other` counts its calls
- **THEN** `Other` has run once, as in .NET, and so it has when the string is interpolated

#### Scenario: A source bound once keeps its type

- **WHEN** a shared component computes `Items().Average()`, where `Items()` answers a `List<int>`
- **THEN** the module eqc writes passes the runtime's strict tsc: the source is bound bare, typed by
  the list, so the callback the lowering hands it is typed too

### Requirement: A LINQ operator takes a comparer the collection fence passes

A comparer handed to `ToDictionary`, `ToLookup`, `GroupBy`, `Distinct`, `ToHashSet` or `SequenceEqual`
SHALL pass the fence a collection's construction passes: one that asks for the key type's default
equality (`EqualityComparer<T>.Default`, a null comparer, or `StringComparer.Ordinal` for a string) SHALL
answer as the operator does without it, its selectors in their parameters whether named or not, and any
other SHALL fail the build with EQ2007, never be dropped or called as a selector.

#### Scenario: An ordinal dictionary

- **WHEN** browser-side code computes `new[] { "a", "bb" }.ToDictionary(w => w, StringComparer.Ordinal)` and reads its `"bb"` entry and its count
- **THEN** the answer is `bb|2`, as in .NET

#### Scenario: A lookup with a comparer

- **WHEN** browser-side code computes `new[] { "a", "bb", "cc" }.ToLookup(w => w.Length, EqualityComparer<int>.Default)` and joins its group of 2
- **THEN** the answer is `bb,cc`, as in .NET

#### Scenario: A comparer that changes equality

- **WHEN** a component computes `words.Distinct(StringComparer.OrdinalIgnoreCase)`
- **THEN** the build fails with EQ2007 at the comparer

#### Scenario: Arguments named out of order

- **WHEN** browser-side code computes `new[] { "a", "bb", "cc" }.GroupBy(comparer: EqualityComparer<int>.Default, keySelector: w => w.Length)` and joins each key with its count
- **THEN** the answer is `1:1,2:2`, as in .NET

#### Scenario: Sequences compared with the default comparer

- **WHEN** browser-side code computes `new[] { 1, 2 }.SequenceEqual(new[] { 1, 3 }, EqualityComparer<int>.Default)`
- **THEN** the answer is false, as in .NET

### Requirement: A class that implements IEnumerable<T> is enumerated as its GetEnumerator() says

A class, a record or a struct that implements `IEnumerable<T>`, or only `IEnumerable`, SHALL be
enumerated in the browser by a `foreach`, a spread, `string.Join` and every LINQ operator as its own
`GetEnumerator()` says, each through the one C# binds for it: a `foreach` over the class through the
type's own public `GetEnumerator()` where it has one, and LINQ, a spread, `string.Join` and a `foreach`
over the interface through the interface's implementation, explicit or not, so that the two walk
different sequences where they answer differently. Through the enumerator an iterator method fills, or
one the app writes, a class that is its own enumerator included, it SHALL be walked by its `MoveNext`
and `Current` and disposed when the walk ends, however it ends. A derived type SHALL be enumerated
through its override of the method. The non-generic `IEnumerable.GetEnumerator()` beside the generic
interface's member, and an explicit `Current` beside a public one, SHALL NOT replace the member that
answers that name.

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

#### Scenario: A public GetEnumerator and an explicit one that walk different sequences

- **WHEN** `class Split : IEnumerable<int>` yields 1 from its public `GetEnumerator()` and 2 from its
  explicit `IEnumerable<int>.GetEnumerator()`
- **THEN** a `foreach` over `new Split()` walks 1, and a `foreach` over it as `IEnumerable<int>`,
  `string.Join`, `Sum()`, a spread and `new List<int>(split)` walk 2, as .NET does, where every one of
  them walked the public one

### Requirement: A sequence its twin would walk otherwise is refused

A twin has one iteration, which LINQ, a spread, `string.Join` and a `foreach` over each interface of
the type walk, and one `Current`. eqc SHALL refuse with EQ1007 a type that implements `IEnumerable<T>`,
or `IEnumerator<T>`, over two types, and a non-generic `IEnumerable.GetEnumerator()` or a `Current` the
twin holds as one with the member it keeps, where that member does not hand back what the kept one
answers. One that returns what the kept member returns, through it, a cast to the interface included,
SHALL be held as one and SHALL NOT be refused.

#### Scenario: Two IEnumerable<T>

- **WHEN** `class Both : IEnumerable<int>, IEnumerable<string>` implements each explicitly
- **THEN** the build fails with EQ1007, naming both interfaces, where LINQ over one of them walked the
  other's sequence

#### Scenario: A non-generic GetEnumerator of its own

- **WHEN** `class Odd : IEnumerable<int>` yields 1 from its public `GetEnumerator()` and `"x"` from its
  explicit `IEnumerable.GetEnumerator()`
- **THEN** the build fails with EQ1007, where a `foreach` over it as `IEnumerable` walked 1 for .NET's `x`

#### Scenario: A Current of its own

- **WHEN** an enumerator's explicit `object IEnumerator.Current => "other";` stands beside its public
  `int Current`
- **THEN** the build fails with EQ1007, where a walk of it as `IEnumerable` read the public one

#### Scenario: Members that hand back the kept one's answer

- **WHEN** `IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();`,
  `IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<string>)this).GetEnumerator();` and
  `object IEnumerator.Current => Current;`
- **THEN** each is held as one with the member it hands back, and the type builds

### Requirement: A LINQ overload answers as its own .NET overload

A LINQ call SHALL be lowered as the overload C# binds, never as a sibling it resembles, and SHALL answer
what that overload answers in .NET. `FirstOrDefault` and `LastOrDefault` handed a default value SHALL
answer the element when there is one, a null element included, and the default otherwise. `Zip(second)`
SHALL answer pairs and `Zip(second, third)` triples, as many as the shortest sequence holds. `Average`
SHALL compute the mean in the element's type: a `long` summed exactly and divided as a double, a `float`
rounded to a single, a `decimal` as a decimal, a nullable over its values alone and null when it has
none, and an empty sequence refused with an `InvalidOperationException` where the answer is not
nullable. `SequenceEqual` SHALL compare by the element type's default equality.

#### Scenario: A default value

- **WHEN** browser-side code computes `new int[0].FirstOrDefault(9)` and `new[] { 1, 2, 3 }.LastOrDefault(x => x > 5, 9)`
- **THEN** both answer 9, as in .NET

#### Scenario: A null element is an element

- **WHEN** browser-side code computes `new string[] { null }.FirstOrDefault("x") ?? "null"`
- **THEN** the answer is `null`, as in .NET

#### Scenario: Three sequences zipped

- **WHEN** browser-side code computes `new[] { 1, 2, 3 }.Zip(new[] { 4, 5, 6 }, new[] { 7, 8, 9 }).Sum(t => t.First * t.Second * t.Third)`
- **THEN** the answer is 270, as in .NET

#### Scenario: The mean of longs

- **WHEN** browser-side code computes `new[] { 1L, 2L, 4L }.Average()` and `new long?[] { 1, null, 4 }.Average()`
- **THEN** the answers are 2.3333333333333335 and 2.5, as in .NET

#### Scenario: An empty mean

- **WHEN** browser-side code computes `new int[0].Average()`
- **THEN** it throws the `InvalidOperationException` .NET throws, with its message

#### Scenario: Sequences of values

- **WHEN** browser-side code computes `new[] { 1.0m }.SequenceEqual(new[] { 1.00m })` and `new[] { double.NaN }.SequenceEqual(new[] { double.NaN })`
- **THEN** both answer true, as in .NET

#### Scenario: Sequences of arrays

- **WHEN** browser-side code computes `new[] { a }.SequenceEqual(new[] { new[] { 1 } })` where `a` is `new[] { 1 }`
- **THEN** the answer is false, as in .NET, which compares the arrays by reference

### Requirement: A grouping in a type-checked module passes tsc

A `GroupBy` or a `ToLookup` in a module eqc writes with type annotations SHALL pass the runtime's
strict `tsc`, its groups read as sequences and its lookup by key.

#### Scenario: A shared component that groups

- **WHEN** a shared component computes `items.GroupBy(x => x).Count()` and `items.ToLookup(x => x)[key]`
- **THEN** the module eqc writes passes the runtime's strict tsc
