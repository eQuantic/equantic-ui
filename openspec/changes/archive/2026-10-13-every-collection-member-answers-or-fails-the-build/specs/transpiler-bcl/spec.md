## ADDED Requirements

### Requirement: A member a collection's twin does not carry fails the build

eqc SHALL lower a member of `Queue<T>`, `Stack<T>`, `LinkedList<T>`, `LinkedListNode<T>` or
`SortedSet<T>` to the runtime twin's member only when the twin carries it, and SHALL refuse any other
member of the five with EQ2004, naming the member, where it went out under its camel-cased name and
failed in the browser. `ToString`, `Equals` and `GetHashCode` SHALL be translated as every object's. A
member reached as a method group SHALL keep its receiver, and a take reached as one SHALL be refused.

#### Scenario: A view of a sorted set

- **WHEN** a component calls `s.GetViewBetween(1, 2)` on a `SortedSet<int>`
- **THEN** the build fails with EQ2004, where the browser threw a TypeError

#### Scenario: A method group

- **WHEN** a component passes `q.Enqueue` to `List<int>.ForEach`
- **THEN** the lowering is `q.enqueue.bind(q)`, and each element is enqueued on `q`
