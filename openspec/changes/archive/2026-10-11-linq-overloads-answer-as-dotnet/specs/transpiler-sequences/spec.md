## ADDED Requirements

### Requirement: A LINQ overload answers as its own .NET overload

A LINQ call SHALL be lowered as the overload C# binds, never as a sibling it resembles, and SHALL answer
what that overload answers in .NET, each argument filling the parameter it binds to however it is
written, named or under a null-conditional. `FirstOrDefault`, `LastOrDefault` and `SingleOrDefault`
handed a default value SHALL answer the element when there is one, a null element included, and the
default otherwise, `SingleOrDefault` refusing a second element or a second match with .NET's
`InvalidOperationException`. `Zip(second)`
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

#### Scenario: A second match

- **WHEN** browser-side code computes `new[] { 1, 2, 3 }.SingleOrDefault(x => x > 1, 9)`
- **THEN** it throws the `InvalidOperationException` .NET throws, "Sequence contains more than one matching element"

#### Scenario: A selector named first

- **WHEN** browser-side code computes `new[] { 0 }.Zip(resultSelector: S(), second: B()).Sum()`, where `S` and `B` record their calls
- **THEN** the answer is 1 and `S` ran before `B`, as in .NET

#### Scenario: A string under a null-conditional

- **WHEN** browser-side code computes `a?.Zip("\U0001F600").Count()` where `a` is `new[] { 1, 2 }`
- **THEN** the answer is 2, the string's two UTF-16 code units, as in .NET

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

## MODIFIED Requirements

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
