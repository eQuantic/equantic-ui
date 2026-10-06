# runtime-sets Specification

## Purpose
What the runtime's set (`utils/hash-set.ts`) answers for a `HashSet<T>`, an `ISet<T>` and an
`IReadOnlySet<T>` on the web: the slots it holds its elements in, the equality it finds them by, its
members and the wire, so a set built, changed and enumerated in the browser answers what .NET's does.

## Requirements

### Requirement: A set holds its elements by slot, as .NET's does

A `HashSet<T>`, an `ISet<T>` and an `IReadOnlySet<T>` SHALL enumerate their elements in slot order: in
insertion order while nothing is removed, with a removed element's slot the next one an insertion
takes, the last slot freed the first reused. Each of .NET's set operations (`UnionWith`,
`IntersectWith`, `ExceptWith`, `SymmetricExceptWith`, `RemoveWhere`) SHALL free and take slots in
.NET's order, `Clear` SHALL free every slot, a copy of a set SHALL keep the set's free slots while the
set is not much larger than it holds, as .NET's copy does, and `TrimExcess` SHALL pack the slots only
where .NET's would.

#### Scenario: An insertion reuses the slot a removal freed

- **WHEN** `var s = new HashSet<int> { 3, 1 }; s.Remove(3); s.Add(5);` runs
- **THEN** `string.Join(",", s)` answers "5,1", as in .NET

#### Scenario: A copy keeps its source's free slot

- **WHEN** a set holding 3 and 1 removes 3, is copied with `new HashSet<int>(a)`, and the copy adds 5 and 6
- **THEN** the copy enumerates "5,1,6", as in .NET

#### Scenario: A set operation frees slots in .NET's order

- **WHEN** a set holding 1 to 5 runs `IntersectWith(new List<int> { 5, 3 })` and then adds 10, 11 and 12
- **THEN** it enumerates "12,11,3,10,5", as in .NET

### Requirement: A set finds an element as the default comparer finds it

A set SHALL find an element as `EqualityComparer<T>.Default` finds it for the element type: by value
for a record, a struct, a tuple, a decimal and a date; by the value's own equality for an element
typed `object`, an interface or a type parameter; a tuple's array element by reference; and NaN equal
to NaN and -0 to 0 for a number. A null SHALL be an element like any other. The equality SHALL be the
one a dictionary's keys take for the same type.

#### Scenario: A date and a decimal

- **WHEN** `new HashSet<DateTime> { new(2026, 1, 1) }.Contains(new DateTime(2026, 1, 1))` and `new HashSet<decimal> { 1.0m, 1.00m }.Count` run
- **THEN** they answer true and 1, as in .NET

#### Scenario: A tuple holding an array

- **WHEN** a `HashSet<(int[], int)>` holding `(a, 1)` is asked whether it contains `(a, 1)` and `(new[] { 1 }, 1)`
- **THEN** it answers true and false, as in .NET

### Requirement: A set's members answer as .NET's

`Add` SHALL answer whether the element was new; `Contains`, `Remove` and `Clear`, the set predicates
(`IsSubsetOf`, `IsProperSubsetOf`, `IsSupersetOf`, `IsProperSupersetOf`, `Overlaps`, `SetEquals`),
`CopyTo`, `EnsureCapacity` and `TrimExcess` SHALL answer and check their arguments as .NET's do, and a
walk over the set SHALL end with .NET's InvalidOperationException when an element is added during it.
A set SHALL be built wherever one is made: a constructor, an initializer, a collection expression and
`ToHashSet`. A member with no form here, `TryGetValue`, SHALL fail the build (EQ1004), and a comparer
that changes what the set considers equal SHALL fail it too (EQ2007).

#### Scenario: The capacity .NET grows to

- **WHEN** `new HashSet<int>().EnsureCapacity(5)` runs
- **THEN** it answers 7, as in .NET

#### Scenario: An addition during a walk

- **WHEN** a `foreach` over a set adds an element
- **THEN** the walk ends with "Collection was modified; enumeration operation may not execute.", as in .NET

### Requirement: A set crosses the wire as its class

A set held in server-rendered state or answered by a Server Action SHALL arrive as the runtime's set,
its elements hydrated and found by its element type's equality, and a set sent to the server SHALL
write itself as the JSON array System.Text.Json reads for it.

#### Scenario: A state field of dates

- **WHEN** a `HashSet<DateTime>` field arrives in server-rendered state
- **THEN** `Contains` answers true for a date the server held
