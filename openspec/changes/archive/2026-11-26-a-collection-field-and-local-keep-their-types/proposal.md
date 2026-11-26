## Why

Closes #1866, a Bug under #1587, found by the review round of #1864.

A field of a queue was declared in TypeScript by a type name its module never imports, so the runtime's `tsc` refused
the module (TS2304):

```csharp
private readonly Queue<Shape> _waiting = new();
```

```ts
_waiting!: Queue<Shape>;
```

A dictionary field beside it was declared `any`, so only some collections met this. A local lost its element type the
other way: `var waiting = new Queue<Shape>();` was written `$eq.collections.queue()`, a `Queue<unknown>` to TypeScript,
so a take into a typed variable was refused (TS2322). An app's bundle strips the types, so its module ran. A twin of the
framework's library holding a queue in a field could not pass the runtime's `tsc`.

## What Changes

For a developer using the SDK nothing they write changes, and no module's JavaScript changes. The library's twins and an
app's modules are TypeScript the runtime's `tsc` takes in these shapes.

- **A field of a queue, a stack, a linked list, its node, a sorted set or a priority queue is declared `any`**, as a
  field of a dictionary already was (`TypeScriptEmitter.IsRuntimeCollectionName`). Decided once for all of them: the
  runtime's classes are not exported by the names a module could import (its `Stack` is the vocabulary's component), and
  a set keeps its `Set<T>`, which the runtime's `HashSet` implements.
- **The runtime's collection factories carry the type arguments of the type they build** (`TsNamedType.TypeArguments`):
  `$eq.collections.queue<Shape>()`, `dictionary<string, number>()`, `hashSetOf<Shape>([…])`, for a construction with
  `new` and for a collection expression alike. Each argument is named as a local's type is, its twin imported; a type
  parameter, a type the build keeps on the server, or a type no module names is `any`, and a nullable reference is
  `T | null`. A local built by one keeps its element type, and a take from it lands in a typed variable. A module written
  without type annotations is JavaScript, where `f<A>()` does not parse, and carries none.

Parts reached: eqc (`TypeScriptEmitter`, `TsNamedType`, `QueueStackStrategy`, `DictionaryStrategy`, `HashSetStrategy`,
`CollectionExpressionStrategy`). No contract surface or developer surface moves. Twenty-two of the runtime's library
modules carry type arguments on their factories, and their JavaScript is unchanged.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `runtime-collections`: a collection's field and local cross to TypeScript as types its module can name.

## Impact

- `SharedComponentTranspilationTests` gains `EmittedWaiting` among its emission shapes: a field and a local of each
  collection, collection expressions, and type arguments a module names without an import of its own. The runtime's `tsc`
  refuses it on main with TS2304 and TS2322.
- The compiler's tests that convert with type annotations pin a factory's text with its type arguments; those that
  convert without them keep the bare factory. `ServerOnlyTypeOfTheAppTests` holds a queue and a dictionary of a kept-out
  type as a free position.
