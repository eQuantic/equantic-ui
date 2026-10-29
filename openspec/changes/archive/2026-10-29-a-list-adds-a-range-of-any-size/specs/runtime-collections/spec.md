## MODIFIED Requirements

### Requirement: A sorted set answers a view of a range

`SortedSet<T>.GetViewBetween(lower, upper)` SHALL answer a sorted set of the elements of its set from
`lower` to `upper`, both included, by the set's order, which shares the set's elements: each SHALL see
what the other changes, and a walk over either SHALL end at any change to the set, inside the range or
not. The view SHALL refuse to add an element outside its range with an ArgumentOutOfRangeException for
`item`, before it changes anything, and SHALL answer an element outside it as not there. Bounds out of
order SHALL be refused with an ArgumentException for `lowerValue`, and a view of a view SHALL be refused
an ArgumentOutOfRangeException for the bound outside the view's range, before the bounds are compared.
The view SHALL take .NET's paths through the set operations: it SHALL never merge, so a `UnionWith` or a
`SymmetricExceptWith` adds element by element and refuses the first outside the range; a set of its
comparer compared with another SHALL be asked for a view of the other's range, which a narrower view
refuses, two sets of one element type, an enum's included, having one comparer; and its `ExceptWith` SHALL ask the count it last counted. The count SHALL be taken again where
.NET reads the view as a collection: reading `Count`, a copy into a list or a set, a list's `AddRange` and
`InsertRange`, and a set's operation that asks the view its count; a walk SHALL NOT take it again. One
divergence is known: LINQ takes it again before every operator, `First` and `Where` included, which only walk
the view in .NET.

#### Scenario: A view, live both ways

- **WHEN** browser-side code takes `v = t.GetViewBetween(2, 6)` of `new SortedSet<int> { 1, 3, 5, 7 }`, adds 4 and 9 to `t`, and reads `v`
- **THEN** `v` holds 3, 4 and 5 and its count is 3, as in .NET; the build failed with EQ2004

#### Scenario: An element outside the range

- **WHEN** browser-side code calls `v.Add(9)` on that view
- **THEN** it throws "Specified argument was out of the range of valid values. (Parameter 'item')" and `t` is unchanged, as in .NET

#### Scenario: A count never read

- **WHEN** browser-side code calls `v.ExceptWith(new[] { 3, 7 })` on a view whose `Count` was never read, and again on one whose `Count` was read
- **THEN** the first removes nothing and the second removes 3, as in .NET

#### Scenario: A set of an enum compared with a view

- **WHEN** browser-side code asks `v.IsSupersetOf(new SortedSet<Rank> { Rank.Zeta, Rank.Mid })` of a view from `Rank.Alpha` to `Rank.Mid`, `Rank.Zeta` ordered before `Rank.Alpha`
- **THEN** it throws "Specified argument was out of the range of valid values. (Parameter 'lowerValue')", as in .NET; it answered false, each set of the enum having a comparer of its own

#### Scenario: A count another set asked

- **WHEN** browser-side code asks `new SortedSet<int> { 3 }.Overlaps(v)` of a view whose `Count` was never read, then calls `v.ExceptWith(new[] { 3 })`
- **THEN** 3 is removed, as in .NET, which asked the view its count

#### Scenario: A view added to a list

- **WHEN** browser-side code calls `new List<int>().AddRange(v)` with a view whose `Count` was never read,
  then `v.ExceptWith(new[] { 3 })`
- **THEN** 3 is removed, as in .NET, whose `AddRange` asked the view its count; nothing was removed
