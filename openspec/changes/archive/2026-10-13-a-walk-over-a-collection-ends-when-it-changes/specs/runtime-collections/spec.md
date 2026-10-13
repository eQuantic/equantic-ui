## ADDED Requirements

### Requirement: A walk over a collection ends when the collection changes

A `foreach` over the runtime's `Queue`, `Stack`, `LinkedList` or `SortedSet`, and over a sorted set's
`Reverse()`, SHALL walk the collection as it is and SHALL end at the step after the collection changes,
the step after the last element included, with .NET's `InvalidOperationException`: in CoreLib's words
for a queue, "Collection was modified; enumeration operation may not execute.", and in System.Collections'
for a stack, a linked list and a sorted set, "Collection was modified after the enumerator was
instantiated." A change SHALL be what .NET counts as one: a queue's `Enqueue`, `Dequeue` and `Clear`; a
stack's `Push`, `Pop` and `Clear`; a linked list's every node added or removed and its `Clear`; a sorted
set's every `Add`, every `Remove` of a set that holds anything, each whether or not it changes the set,
its `Clear`, and its set operations along the path .NET takes through them. A read SHALL be no change,
and neither SHALL a value written into a linked list's node. A sorted set's `Reverse()` SHALL walk from
the largest element and SHALL read the set when it is walked, each time it is.

#### Scenario: A walk that adds as it goes

- **WHEN** browser-side code runs `foreach (var x in s) s.Add(x + 10);` over `new SortedSet<int> { 1, 3, 5 }`
- **THEN** it throws an InvalidOperationException, "Collection was modified after the enumerator was instantiated.", after the first element, as in .NET, where the walk never ended

#### Scenario: A change after the last element

- **WHEN** browser-side code walks `new Queue<int>(new[] { 1, 2, 3 })` and clears the queue at its last element
- **THEN** the walk sees 1, 2 and 3 and then throws an InvalidOperationException, "Collection was modified; enumeration operation may not execute.", as in .NET

#### Scenario: An Add that changes nothing

- **WHEN** browser-side code adds each element of `new SortedSet<int> { 1, 3, 5 }` to the set again while walking it
- **THEN** the walk throws after the first element, as in .NET

#### Scenario: A set operation that asks first

- **WHEN** browser-side code calls `ExceptWith(new[] { 2 })` at every element of `new SortedSet<int> { 1, 3, 5 }` while walking it
- **THEN** the walk sees 1, 3 and 5 and ends, as in .NET, where `ExceptWith(new SortedSet<int> { 2 })` ends it after the first element

#### Scenario: A node's value written in place

- **WHEN** browser-side code walks `new LinkedList<int>(new[] { 1, 2, 3 })` and writes 9 into its last node at the first element
- **THEN** the walk sees 1, 2 and 9, as in .NET

#### Scenario: A sorted set walked backwards

- **WHEN** browser-side code takes `s.Reverse()` of `new SortedSet<int> { 1, 3, 5 }`, adds 7 to the set, and walks the reverse twice
- **THEN** each walk sees 7, 5, 3 and 1, as in .NET, where `Reverse()` was a TypeError
