## Why

Closes #746, a bug under #849 (the transpiler's fences hold on every path).

C# compiles `e.Changed += h` into a call of the event's `add` accessor: a field-like event's is written by the
compiler and composes the event's invocation list, and an event declared with `add` and `remove` brings its own.
The emitter knew only field-like events. An event declared with accessors had no twin, and a subscription wrote
`x.changed = $eq.delegates.combine(x.changed, h)`, a key of its name the accessors never read. An explicit
interface event always has accessors, so a handler subscribed through the interface was never raised: measured
through the conformance harness, .NET counted 105 and the browser 5.

Proving it found a second defect beneath: `+=` and `-=` on a delegate field, a local or a property were emitted
literally, so the backing field an event's accessors compose into held the text of its handlers, and the event
threw when raised. C# composes a delegate's invocation list there exactly as it does an event's.

## What Changes

- An event declared with accessors is written as two methods of its twin, `$add$<event>(value)` and
  `$remove$<event>(value)`, names no C# member can take, on the class for a static event (`EventAccessors`).
- A subscription reaches the accessors of the event it binds: a field-like event's list is composed in place as
  before, an event the source declares with accessors has its methods called, and an interface's, an abstract or
  a virtual event, whose implementation may be either, goes through `$eq.delegates.addToEvent` and
  `removeFromEvent`, which call the accessors the twin wrote and compose the list otherwise.
- `+=` and `-=` on a delegate compose its invocation list, as on an event.
- An app writes nothing new. `eQuantic.UI.Compiler` is a tool surface (two `Eq` names are new); the runtime's
  `$eq.delegates` gains two functions. No contract and no developer surface moves.

## Capabilities

### Modified Capabilities

- `transpiler-classes`: an event's accessors are called as C# calls them, and `+=` composes a delegate.

## Impact

`eQuantic.UI.Compiler` (`EventSubscriptionStrategy`, `EventAccessors`, `TypeScriptEmitter`, `Eq`) and the runtime
(`utils/delegates.ts`, `eq.ts`).
