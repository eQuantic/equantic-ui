## ADDED Requirements

### Requirement: A take answers whether there was an element and writes it

A queue's `TryDequeue` and `TryPeek`, a stack's `TryPop` and `TryPeek`, and a sorted set's
`TryGetValue` SHALL answer as .NET's do: true and the element when there is one, and false and the
type's default otherwise, written into the `out` argument whatever it held. The argument SHALL be
written in place, whether it is a variable, a discard, an array element or a field, each part of it
read once. A sorted set's `TryGetValue` SHALL write the element the set holds, not the one it was
asked for.

#### Scenario: A queue drained

- **WHEN** browser-side code runs `while (q.TryDequeue(out var x)) s += x;` over `new Queue<int>(new[] { 1, 2, 3 })`
- **THEN** `s` is `123`, as in .NET; in the browser `tryDequeue` was not a function

#### Scenario: Nothing to take

- **WHEN** browser-side code calls `TryPop(out y)` on an empty `Stack<int>`, where `y` held 8
- **THEN** the answer is False and `y` is 0, as in .NET

#### Scenario: The element the set holds

- **WHEN** browser-side code calls `TryGetValue(1.00m, out var x)` on `new SortedSet<decimal> { 1.0m, 2m }`
- **THEN** the answer is True and `x` prints `1.0`, as in .NET

### Requirement: A queue and a stack keep .NET's capacity

A queue and a stack SHALL answer `Capacity`, `EnsureCapacity` and `TrimExcess` as .NET's do. A queue
SHALL grow to twice its capacity, four more at least, and a stack to twice its capacity, four from
empty, each to the capacity asked for at least. A construction from a sequence SHALL size the
collection to the count of an `ICollection<T>`, and by doubling from four for any other sequence, a
queue, a stack and a string among them. `TrimExcess()` SHALL trim to the count below nine tenths of
the capacity, and `TrimExcess(capacity)` to the capacity it is handed. A negative capacity, and a
`TrimExcess(capacity)` below the count, SHALL be refused with .NET's `ArgumentOutOfRangeException`. A
queue's capacity changed SHALL end a walk over it, and a stack's SHALL NOT, as in .NET.

#### Scenario: A queue grows

- **WHEN** browser-side code enqueues six values on `new Queue<int>(1)`, reading `Capacity` after each
- **THEN** it reads 1, 5, 5, 5, 5 and 10, as in .NET; in the browser `capacity` was undefined

#### Scenario: A capacity from a sequence

- **WHEN** browser-side code reads the `Capacity` of `new Queue<char>("abcde")` and of `new Queue<int>(new List<int> { 1, 2 })`
- **THEN** it reads 8 and 2, as in .NET

#### Scenario: A trim below the count

- **WHEN** browser-side code calls `TrimExcess(1)` on a stack of two elements
- **THEN** it throws the `ArgumentOutOfRangeException` .NET throws, "capacity ('1') must be greater than or equal to '2'."

### Requirement: A linked list links by its nodes as .NET's does

A linked list SHALL answer `Find`, `FindLast`, `AddAfter`, `AddBefore`, the `AddFirst`, `AddLast` and
`Remove` that take a node, and `CopyTo` as .NET's do, and a node SHALL answer `List`, `Next`,
`Previous` and `Value`. `new LinkedListNode<T>(value)` SHALL build a node that belongs to no list. A
node removed from its list, by itself, by its value, from either end or by `Clear`, SHALL belong to no
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

#### Scenario: A node already linked

- **WHEN** browser-side code calls `l.AddLast(l.First!)`
- **THEN** it throws the `InvalidOperationException` .NET throws, "The LinkedList node already belongs to a LinkedList."
