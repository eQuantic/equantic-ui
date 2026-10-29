## ADDED Requirements

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
