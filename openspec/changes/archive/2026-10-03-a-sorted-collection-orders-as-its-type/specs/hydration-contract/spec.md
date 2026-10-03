## MODIFIED Requirements

### Requirement: A collection crosses as the class the browser's code reads

A component's value of a collection type the browser holds as one of its own classes SHALL reach the
browser's code as that class, whatever the server's serializer wrote: a `HashSet`, an `ISet` or an
`IReadOnlySet` as a set, and a `SortedSet`, a `Queue`, a `Stack` and a `LinkedList` as the
runtime's. Each element SHALL be hydrated by its own type, and the collection SHALL enumerate and
take its elements in the order it does on the server, a sorted one in its element type's order. A set
or a dictionary SHALL NOT cross when the browser's copy would answer differently: one holding a
comparer other than its element type's default equality (or the ordinal one, for a string), a sorted
one holding a comparer other than its element type's default order, read through a wrapper as well,
and, in a member the browser rebuilds as a set or a dictionary, one of a class whose equality cannot
be read. The server SHALL leave it out and log it; in a projected value, only the read that ends at it
SHALL be left out.

#### Scenario: A set a prefetch loaded

- **WHEN** a page's `PrefetchAsync` sets a `HashSet<string>` field to `admin` and `editor`, and its
  `Build` writes `_roles.Contains("admin")` and `_roles.Count`
- **THEN** the browser's first build after hydration, and after a client navigation, writes `True` and
  `2`, as the server did

#### Scenario: A stack keeps its top

- **WHEN** a prefetch pushes 1, 2 and 3 onto a `Stack<int>` field, and the page writes `_stack.Peek()`
- **THEN** the browser writes `3`, as the server did

#### Scenario: A sorted set keeps its order

- **WHEN** a prefetch adds `b`, `B` and `a` to a `SortedSet<string>` field and `10m`, `9m`, `1.0m` and
  `1.00m` to a `SortedSet<decimal>` field, and the page writes each one out in a loop
- **THEN** the browser writes `a`, `b`, `B` and three decimals in the order `1`, `9`, `10`, as the
  server did

#### Scenario: A set with its own comparer

- **WHEN** a prefetch sets a `HashSet<string>` field to one made with `StringComparer.OrdinalIgnoreCase`,
  an `IReadOnlySet<string>` field to a case-insensitive `ImmutableHashSet` or to a set of the app's own,
  or a `SortedSet<string>` field to one ordered by `StringComparer.Ordinal`
- **THEN** the served payload carries nothing of that field, and the server's log names it

#### Scenario: A projected value keeps the rest

- **WHEN** a container-provided value the page reads holds a name and a case-insensitive set
- **THEN** the served payload carries its name and nothing of the set, and the server's log names the
  read

#### Scenario: A queue of longs

- **WHEN** a prefetch enqueues `9007199254740993` and `2` on a `Queue<long>` field, and the page writes
  `_queue.Peek() + 1`
- **THEN** the browser writes `9007199254740994`, as the server did
