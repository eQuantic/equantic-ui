## ADDED Requirements

### Requirement: A sorted set holds .NET's red-black tree

The runtime's `SortedSet` SHALL hold its elements in the red-black tree .NET's `SortedSet<T>` holds them
in, built by the same steps: an `Add` and a `Remove` as .NET's top-down insertion and removal, a
construction from a sequence and a merging set operation as .NET's construction from a sorted array, and
a copy of a sorted set of the same comparer as .NET's copy of its tree. `RemoveWhere` SHALL ask its
predicate in the breadth-first order of that tree, a view's from the root of its range, and
`new SortedSet<T>(collection)` SHALL keep, of elements its comparer finds equal, the one .NET's sort puts
first.

#### Scenario: The order RemoveWhere asks in

- **WHEN** browser-side code records each element `RemoveWhere` asks of `new SortedSet<int> { 1, 2, 3, 4, 5, 6, 7 }`
- **THEN** the record is `2,1,4,3,6,5,7`, as in .NET, where it was `1,2,3,4,5,6,7`

#### Scenario: A set built from a list

- **WHEN** browser-side code records each element `RemoveWhere` asks of `new SortedSet<int>(new List<int> { 5, 3, 8, 1, 4, 7, 9, 2, 6, 3 })`
- **THEN** the record is the breadth-first order of the tree .NET builds from the sorted array, as in .NET
