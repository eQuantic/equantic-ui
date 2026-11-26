## ADDED Requirements

### Requirement: A collection crosses to TypeScript as a type its module can name

eqc SHALL declare a field of a queue, a stack, a linked list, a linked list's node, a sorted set or a priority queue as
`any` in TypeScript, as it declares a field of a dictionary, and a field of a set as `Set<T>`, so that no module names a
collection type it does not import. Every construction of one of the runtime's collections, with `new` or by a
collection expression, SHALL hand the runtime's factory the type arguments of the type it builds, each named as a
local's type is named, `any` for a type parameter or for a type no module names, and `T | null` for a nullable
reference, so a local built by one keeps its element type.

#### Scenario: A field of each collection passes tsc

- **WHEN** a class holds a field of a queue, a stack, a linked list, a sorted set and a set of an app's type
- **THEN** its module declares each field by a type it can name, and the runtime's `tsc` takes the module

#### Scenario: A local keeps its element type

- **WHEN** a method builds `var waiting = new Queue<Shape>()` and takes from it with `waiting.TryDequeue(out _next)`
  into a field of type `Shape?`
- **THEN** the module writes `$eq.collections.queue<Shape>()`, and the runtime's `tsc` takes the take into the field

#### Scenario: A type argument no module names

- **WHEN** a method builds a `Stack<T>` of its own type parameter, a `Queue<Shape?>` and a `Dictionary<DateTime, decimal>`
- **THEN** the factories read `stack<any>()`, `queue<Shape | null>()` and `dictionary<DateTime, Decimal>(…)`, the module
  imports `DateTime` and `Decimal` from the runtime, and the runtime's `tsc` takes it
