# runtime-collections Specification

## Purpose
What the runtime's `Queue`, `Stack`, `LinkedList` and `SortedSet` answer on the web, starting with
the JSON each writes when it crosses the wire.

## Requirements

### Requirement: A collection writes the JSON .NET writes

The runtime's `Queue`, `Stack`, `LinkedList` and `SortedSet` SHALL write themselves to JSON as
System.Text.Json writes them: an array of their elements in the order each enumerates, a stack from
its top.

#### Scenario: A queue

- **WHEN** `new Queue<long>(new[] { 1L, 2L })` is written to JSON
- **THEN** it is `["1","2"]` on both sides, a long as a BigInt's digits, where the browser wrote `{"items":[...]}`

#### Scenario: A stack

- **WHEN** `new Stack<int>(new[] { 1, 2, 3 })` is written to JSON
- **THEN** it is `[3,2,1]`, as in .NET

### Requirement: A queue, a stack and a sorted set copy and combine as .NET's do

The runtime's `Queue` and `Stack` SHALL answer `CopyTo`, and its `SortedSet` SHALL answer `CopyTo`,
`UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith`, `IsSubsetOf`, `IsProperSubsetOf`,
`IsSupersetOf`, `IsProperSupersetOf`, `Overlaps`, `SetEquals` and `RemoveWhere`, each as .NET answers
it: the same elements in the same order, the same refusals in the same words, and an argument that
repeats an element, the empty set and the set itself handled as .NET handles them. Of two elements the
set's comparer finds equal, a union and an intersection SHALL keep the instance .NET keeps.

#### Scenario: A queue's copy

- **WHEN** browser-side code dequeues 1 from `new Queue<int>(new[] { 1, 2, 3 })`, enqueues 4, and copies it into `new int[5]` from index 1
- **THEN** the array is `0,2,3,4,0`, as in .NET

#### Scenario: A stack's copy

- **WHEN** browser-side code pushes 1, 2 and 3 onto a stack and copies it into `new int[4]` from index 1
- **THEN** the array is `0,3,2,1`, as in .NET

#### Scenario: A copy refused

- **WHEN** browser-side code copies a sorted set of three elements into `new int[4]` from index 2
- **THEN** it throws the `ArgumentException` .NET throws, "Destination array is not long enough to copy all the items in the collection. Check array index and length."

#### Scenario: A symmetric difference over repeated elements

- **WHEN** browser-side code computes `SymmetricExceptWith(new[] { 4, 2, 4, 2, 5 })` over `new SortedSet<int> { 1, 2, 3 }`
- **THEN** the set is `1,3,4,5`, as in .NET

#### Scenario: The instance a merge keeps

- **WHEN** browser-side code unions `new SortedSet<decimal> { 1.0m, 2m }` with `new SortedSet<decimal> { 1.00m, 3m }`
- **THEN** the set prints `1.00,2,3`, as in .NET

#### Scenario: The instance an addition keeps

- **WHEN** browser-side code unions `new SortedSet<decimal> { 1.0m, 2m }` with `new List<decimal> { 1.00m, 3m }`
- **THEN** the set prints `1.0,2,3`, as in .NET

#### Scenario: A question over repeated elements

- **WHEN** browser-side code asks `new SortedSet<int> { 1, 2 }` whether it `IsProperSubsetOf(new[] { 2, 1, 1 })` and whether it `SetEquals(new[] { 2, 1, 2 })`
- **THEN** the answers are False and True, as in .NET

#### Scenario: A null argument

- **WHEN** browser-side code calls `UnionWith(null)` on an empty sorted set
- **THEN** it throws the `ArgumentNullException` .NET throws, naming `other`

### Requirement: A collection's constructor refuses a null source by its parameter's name

A construction of a `List`, `Queue`, `Stack`, `LinkedList`, `SortedSet`, `Dictionary`,
`SortedDictionary` or `SortedList` that copies a source SHALL refuse a null one with .NET's
`ArgumentNullException`, naming the parameter the source binds to (`collection` or `dictionary`), and a
`Queue` or a `Stack` SHALL refuse a negative capacity in .NET's words. A string copied into a
collection SHALL be read by its UTF-16 chars.

#### Scenario: A null collection

- **WHEN** browser-side code builds `new Queue<int>(source)` where `source` is a null `IEnumerable<int>`
- **THEN** it throws the `ArgumentNullException` .NET throws, "Value cannot be null. (Parameter 'collection')"

#### Scenario: A null dictionary

- **WHEN** browser-side code builds `new Dictionary<string, int>(source, StringComparer.Ordinal)` where `source` is a null `IDictionary<string, int>`
- **THEN** it throws the `ArgumentNullException` .NET throws, naming `dictionary`

#### Scenario: A null list source

- **WHEN** browser-side code builds `new List<int>(source)` where `source` is a null `IEnumerable<int>`
- **THEN** it throws the `ArgumentNullException` .NET throws, naming `collection`, where a spread threw a `TypeError`

#### Scenario: A string's chars

- **WHEN** browser-side code counts `new List<char>("a\U0001F600b")`
- **THEN** the answer is 4, as in .NET

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
- **THEN** it throws an InvalidOperationException, "Collection was modified after the enumerator was instantiated.", after the first element, as in .NET; the browser's walk never ended

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
- **THEN** each walk sees 7, 5, 3 and 1, as in .NET; in the browser `Reverse()` was a TypeError

### Requirement: A take answers whether there was an element and writes it

A queue's `TryDequeue` and `TryPeek`, a stack's `TryPop` and `TryPeek`, and a sorted set's
`TryGetValue` SHALL answer as .NET's do: true and the element when there is one, and false and the
type's default otherwise, written into the `out` argument whatever it held. The argument SHALL be
written in place, whether it is a variable, a discard, an array element or a field, each part of it
read once, after the receiver, which SHALL be read before every argument as C# reads it, an `out`
named before the value included. A sorted set's `TryGetValue` SHALL write the element the set holds,
not the one it was asked for.

#### Scenario: A queue drained

- **WHEN** browser-side code runs `while (q.TryDequeue(out var x)) s += x;` over `new Queue<int>(new[] { 1, 2, 3 })`
- **THEN** `s` is `123`, as in .NET; in the browser `tryDequeue` was not a function

#### Scenario: Nothing to take

- **WHEN** browser-side code calls `TryPop(out y)` on an empty `Stack<int>`, where `y` held 8
- **THEN** the answer is False and `y` is 0, as in .NET

#### Scenario: The element the set holds

- **WHEN** browser-side code calls `TryGetValue(1.00m, out var x)` on `new SortedSet<decimal> { 1.0m, 2m }`
- **THEN** the answer is True and `x` prints `1.0`, as in .NET

#### Scenario: The receiver before an out named first

- **WHEN** browser-side code calls `Get().TryGetValue(actualValue: out slots[i++], equalValue: Key())`, where `Get` and `Key` each log `i`
- **THEN** the log is `g0k1`, as in .NET; in the browser the receiver was read after `i++`, and logged `g1`

### Requirement: An empty sorted set's ends are its element type's default

A sorted set's `Min` and `Max` SHALL answer its least and its greatest element, and its element
type's default when it holds nothing, as .NET's do, whether they are read by a member access, through
`?.` or by a property pattern. Through `?.`, a set that is null SHALL answer null.

#### Scenario: The least of nothing

- **WHEN** browser-side code computes `new SortedSet<int>().Min + 1`
- **THEN** it is 1, as in .NET; in the browser it was NaN

#### Scenario: The least of nothing, through `?.`

- **WHEN** browser-side code computes `s?.Min + 1`, where `s` is an empty `SortedSet<int>`, and `none?.Max + 1 ?? -1`, where `none` is a `SortedSet<int>` that is null
- **THEN** they are 1 and -1, as in .NET; in the browser the first was null

### Requirement: A queue and a stack keep .NET's capacity

A queue and a stack SHALL answer `Capacity`, `EnsureCapacity` and `TrimExcess` as .NET's do. A queue
SHALL grow to twice its capacity, four more at least, and a stack to twice its capacity, four from
empty, each to the capacity asked for at least. A construction from a sequence SHALL size the
collection to the count of an `ICollection<T>`, the runtime's or a type of the app's own, and by
doubling from four for any other sequence, a queue, a stack, a string and a type that counts its
elements without being an `ICollection<T>` among them. `TrimExcess()` SHALL trim to the count below nine tenths of
the capacity, and `TrimExcess(capacity)` to the capacity it is handed. A negative capacity, and a
`TrimExcess(capacity)` below the count, SHALL be refused with .NET's `ArgumentOutOfRangeException`. A
queue's capacity changed SHALL end a walk over it, and a stack's SHALL NOT, as in .NET. A queue and a
stack read from the JSON the server wrote SHALL grow one element at a time, as System.Text.Json reads
them, since the payload carries no capacity.

#### Scenario: A queue grows

- **WHEN** browser-side code enqueues six values on `new Queue<int>(1)`, reading `Capacity` after each
- **THEN** it reads 1, 5, 5, 5, 5 and 10, as in .NET; in the browser `capacity` was undefined

#### Scenario: A capacity from a sequence

- **WHEN** browser-side code reads the `Capacity` of `new Queue<char>("abcde")` and of `new Queue<int>(new List<int> { 1, 2 })`
- **THEN** it reads 8 and 2, as in .NET

#### Scenario: A counted sequence that is no collection

- **WHEN** browser-side code reads the `Capacity` of `new Queue<int>(ring)`, where `ring` is an app's `IReadOnlyList<int>` of three elements and no `ICollection<int>`
- **THEN** it reads 4, as in .NET; in the browser it read the ring's count, 3

#### Scenario: A queue read from the server's JSON

- **WHEN** the browser hydrates a `Queue<int>` the server wrote as `[1]`
- **THEN** its `Capacity` is 4, as .NET's `JsonSerializer.Deserialize<Queue<int>>("[1]")` holds it, where it was 1

#### Scenario: A trim below the count

- **WHEN** browser-side code calls `TrimExcess(1)` on a stack of two elements
- **THEN** it throws the `ArgumentOutOfRangeException` .NET throws, "capacity ('1') must be greater than or equal to '2'."

### Requirement: A linked list links by its nodes as .NET's does

A linked list SHALL answer `Find`, `FindLast`, `AddAfter`, `AddBefore`, the `AddFirst`, `AddLast` and
`Remove` that take a node, and `CopyTo` as .NET's do, and a node SHALL answer `List`, `Next`,
`Previous` and `Value`. `new LinkedListNode<T>(value)` SHALL build a node that belongs to no list, and
an object initializer SHALL set its `Value` after the construction. A node removed from its list, by itself, by its value, from either end or by `Clear`, SHALL belong to no
list and link to nothing. A node that is null SHALL be refused with .NET's `ArgumentNullException`
naming `node`, one of another list or of none with "The LinkedList node does not belong to current
LinkedList.", and a new node that already belongs to a list with "The LinkedList node already belongs
to a LinkedList.", each as an `InvalidOperationException`.

#### Scenario: A node removed

- **WHEN** browser-side code removes `l.First!.Next!` from `new LinkedList<int>(new[] { 1, 2, 3 })`
- **THEN** the list is `1,3` and the node's `List` is null, as in .NET; in the browser the node was looked for among the values and nothing was removed

#### Scenario: A node of its own

- **WHEN** browser-side code adds `new LinkedListNode<int>(1)` before the first node of `new LinkedList<int>(new[] { 2 })`
- **THEN** the list is `1,2` and the node's `List` is the list, as in .NET

#### Scenario: A node built with an object initializer

- **WHEN** browser-side code builds `new LinkedListNode<int>(1) { Value = 2 }`
- **THEN** its `Value` is 2, as in .NET; in the browser the initializer went out as a call to an `add` no node has

#### Scenario: A node already linked

- **WHEN** browser-side code calls `l.AddLast(l.First!)`
- **THEN** it throws the `InvalidOperationException` .NET throws, "The LinkedList node already belongs to a LinkedList."

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

### Requirement: An empty collection expression builds the queue, the stack or the linked list its target names

A collection expression whose target is a `Queue`, a `Stack` or a `LinkedList` SHALL build on the web the
runtime's class `new()` builds for that type, wherever it is written, and the capacity `with(…)` hands the
constructor SHALL be the queue's or the stack's capacity, as `new(capacity)` makes it. An element, which only an
`Add` of the app's own can add to one of these types, SHALL fail the build with EQ1004.

#### Scenario: A queue started empty

- **WHEN** browser-side code runs `Queue<int> q = []; q.Enqueue(1); q.Enqueue(2); return q.Dequeue() + "," + q.Count;`
- **THEN** it answers "1,1", as in .NET, where `q.enqueue` was not a function

#### Scenario: A stack's capacity handed by with

- **WHEN** browser-side code runs `Stack<int> s = [with(3)]; return s.Capacity;`
- **THEN** it answers 3, as in .NET

#### Scenario: An element an extension adds

- **WHEN** an app declares `Add(this Queue<int>, int)` and writes `Queue<int> q = [1, 2];` in code that crosses
- **THEN** the build fails with EQ1004

#### Scenario: A linked list started empty

- **WHEN** browser-side code runs `LinkedList<int> l = []; l.AddLast(1); l.AddFirst(0);` and joins it
- **THEN** it reads "0,1", as in .NET
