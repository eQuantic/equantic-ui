## MODIFIED Requirements

### Requirement: A member a collection's twin does not carry fails the build

eqc SHALL lower a member of `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `LinkedListNode<T>` or
`SortedSet<T>` to the runtime twin's member only when the twin carries it, and SHALL refuse any other
member of the five with EQ2004, naming the member; it used to go out under its camel-cased name and
fail in the browser. `ToString`, `Equals` and `GetHashCode` SHALL be translated as every object's. A
member reached as a method group SHALL keep its receiver, and a take reached as one SHALL be refused.
A sorted set's `GetViewBetween` SHALL be the twin's.

#### Scenario: A sorted set's comparer

- **WHEN** a component reads `s.Comparer` on a `SortedSet<int>`
- **THEN** the build fails with EQ2004

#### Scenario: A view of a sorted set

- **WHEN** a component calls `s.GetViewBetween(1, 2)` on a `SortedSet<int>`
- **THEN** the lowering is `s.getViewBetween(1, 2)`, where the build failed with EQ2004

#### Scenario: A method group

- **WHEN** a component passes `q.Enqueue` to `List<int>.ForEach`
- **THEN** the lowering is `q.enqueue.bind(q)`, and each element is enqueued on `q`
