## ADDED Requirements

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
