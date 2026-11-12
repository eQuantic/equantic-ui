## Why

Closes #1445, a bug left by #746's fix, under #849 (the transpiler's fences hold on every path).

#746 made a subscription to an event declared with `add` and `remove` accessors call two methods of its twin,
`$add$<event>` and `$remove$<event>`. Only the plain-class path of `TypeScriptEmitter` writes those methods. The
record and struct writer (`RecordTypeEmitter`) and the component writer (`TypeScriptEmitter.Emit(ComponentDefinition)`)
write none, while the call site, which asks only how the event is declared (`EventAccessors.Of`), calls them on
every receiver. So a subscription to a record's, a struct's or a component's event with accessors throws
`TypeError: s.$add$fired is not a function` in the browser where .NET counts. Measured by the conformance harness,
both modes: .NET 21, 52 and 52, JavaScript "threw" three times.

## What Changes

- One writer of an event's accessor methods, `MethodLowering.EventAccessors`, beside its sibling
  `MethodLowering.Indexer`, which already serves "a class, a component, a record, a struct". Every emitter that
  writes a twin calls it: the class and static-class path (as now), the record and struct writer, and the
  component writer. `TypeScriptEmitter.EmitEventAccessors` goes; its body is the new writer's.
- The new writer asks no `[ServerOnly]` question. `ComponentParser.StaysOnTheServer` answers false for an
  `EventDeclarationSyntax` whatever its `inAComponent` argument, and `[ServerOnly]` cannot mark an event (CS0592,
  measured), so the call the class path makes today decides nothing.
- An app writes nothing new: the subscription it already wrote runs. No contract surface
  (`PublicAPI.Unshipped.txt`) and no developer surface (`developer-surface.baseline.txt`) moves:
  `eQuantic.UI.Compiler` is a tool surface, and every member touched is internal.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: an event's accessors are written into the twin of whatever type declares the event, a
  record's, a struct's and a component's included.

## Impact

eqc only: `eQuantic.UI.Compiler` (`MethodLowering`, `TypeScriptEmitter`, `RecordTypeEmitter`). No runtime, no
realizer, no Photon shell, no SDK, no template. The transpiled pins move only for a type that declares an event
with accessors.

Principles followed: 9 (the structural fix, one writer per level: the transpiler is the bar), 11 (the writer is a
member of the lowering both emitters already share, not a new static helper), 13 (the class path's private writer
goes, nothing kept beside the new one), 14 (the gap is fixed in the SDK, the C# is the C# a developer writes). None
is bent.
