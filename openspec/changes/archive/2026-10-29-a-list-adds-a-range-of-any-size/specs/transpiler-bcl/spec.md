## ADDED Requirements

### Requirement: A list adds and inserts a range of any size as .NET's does

`List<T>.AddRange(collection)` and `InsertRange(index, collection)` SHALL take a collection of any size,
adding its elements in the order it enumerates, a string's UTF-16 chars as its elements, and SHALL never
hand the elements to one call as its arguments, which every engine caps. A null collection SHALL be refused
with an ArgumentNullException for `collection`, before the index is checked, and an index below zero or past
the count SHALL be refused with an ArgumentOutOfRangeException for `index`. A list that is its own range SHALL
add or insert what it held before the call, and each argument SHALL be evaluated once, in the order it is
written.

#### Scenario: A million elements

- **WHEN** `list.AddRange(items)` runs for a list of one element and an array of a million, and
  `InsertRange(1, items)` for a list of two
- **THEN** the lists hold 1,000,001 and 1,000,002 elements in .NET's order; both threw a RangeError in the
  browser

#### Scenario: An index outside the list

- **WHEN** `new List<int> { 1 }.InsertRange(2, new[] { 9 })` runs
- **THEN** it throws "Index was out of range. Must be non-negative and less than or equal to the size of the
  collection. (Parameter 'index')", as in .NET; it inserted 9 at the end

#### Scenario: Named arguments out of order

- **WHEN** `L().InsertRange(collection: C(), index: I())` runs, each call recording its letter
- **THEN** the letters are "lci" and the collection is inserted at the index, as in .NET; the index was
  spread as the collection
