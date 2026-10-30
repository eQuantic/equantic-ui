## ADDED Requirements

### Requirement: A list inserts, removes and takes at a position as .NET's does

`List<T>.Insert(index, item)`, `RemoveAt(index)`, `RemoveRange(index, count)` and `GetRange(index, count)`,
and `IList<T>.Insert` and `RemoveAt`, SHALL take each argument in its parameter's place, evaluated once in
the order it is written, and SHALL refuse a position .NET refuses with its exception and its words: an
`Insert` index below zero or past the count with an ArgumentOutOfRangeException for `index` ("Index must
be within the bounds of the List."), a `RemoveAt` index below zero or at the count or past it with one for
`index`, and for `RemoveRange` and `GetRange` a negative index or count with one for that parameter and a
range past the end with an ArgumentException. A null list SHALL throw a NullReferenceException before its
arguments are checked.

#### Scenario: Named arguments

- **WHEN** `l.Insert(item: 9, index: 0)` runs over `{ 1, 2 }`
- **THEN** the list holds `9,1,2`, as in .NET; it held `1,2,0`

#### Scenario: A position past the end

- **WHEN** `l.RemoveAt(5)` runs over `{ 1, 2, 3 }`, or `l.GetRange(0, 5)` over `{ 1 }`
- **THEN** the first throws "Index was out of range. Must be non-negative and less than the size of the
  collection. (Parameter 'index')" and the second "Offset and length were out of bounds for the array or
  count is greater than the number of elements from index to the end of the source collection.", as in
  .NET; the first removed nothing and the second answered a list of one

#### Scenario: An index read once

- **WHEN** `l.GetRange(i++, 2)` runs over `{ 1, 2, 3 }` with `i` at 0
- **THEN** it answers `1,2` and `i` is 1, as in .NET; `i` was read twice
