## Context

Which tree: none of Flutter's three. This is the transpiler writing a C# type's twin; Flutter compiles Dart and has
no twin, and `docs/FLUTTER-PARITY.md` has no row for it. The answer is C#'s: `e.Fired += h` calls the event's add
accessor, whatever kind of type declares the event.

Three writers write a twin with members (`ComponentCompiler` picks one per type):

| Twin | Writer | Writes an event's accessors today |
|---|---|---|
| class, static class | `TypeScriptEmitter.EmitClassModule` → `EmitStaticMembers` → `EmitEventAccessors` | yes |
| record, struct | `RecordTypeEmitter.EmitModule` | no |
| component, stateful and stateless | `TypeScriptEmitter.Emit(ComponentDefinition)` | no |

A union's twin is a type alias, and an extension block refuses an event (EQ2008): neither writes members.

## Goals / Non-Goals

**Goal:** every twin that holds members holds the accessor methods every subscription to its event calls.

**Non-goals:** a field-like event on a component (the parser has no event case; not measured here), a struct copied
before a subscription, an event of a nested type, `[ServerOnly]` on an accessor (see Open).

## Decisions

### One writer, on `MethodLowering`, beside `Indexer`

`MethodLowering` is the lowering both emitters already hold (`TypeScriptEmitter.Lowering`,
`RecordTypeEmitter._lowering`), with the converter, the annotation switch and the model. Its `Indexer` is the exact
sibling: an accessor-bodied member turned into `JsClassMember`s, "one path for a class, a component, a record, a
struct and an interface's default", called by each emitter with its own `declaredType`. The new member imitates it:

```csharp
public IEnumerable<JsClassMember> EventAccessors(EventDeclarationSyntax e, Func<TypeSyntax?, string> declaredType, bool asStatic)
```

It yields `$add$<twin>` and `$remove$<twin>` (`CodeGen.EventAccessors.Name`, the twin name from
`IEventSymbol.MemberTwinName()`), `static` for a static event or a static class, each with its `Origin` on the
accessor, the body from `AccessorBody` or `ExpressionBody(…, returns: false)`, exactly what `EmitEventAccessors`
writes today. Callers:

- `EmitStaticMembers`: `c.Member(member, member.Origin?.Member)` for each, where the loop over
  `EventDeclarationSyntax` is now (`TypeScriptEmitter.cs` ~1903).
- `RecordTypeEmitter.EmitModule`: `sb.Append(Written(member))`, after the indexers, as they are written.
- `Emit(ComponentDefinition)`: over `component.ClassSyntax`, inside the `_builder.Class(…)` callback, with
  `asStatic: false`.

`TypeScriptEmitter.EmitEventAccessors` is deleted. Rejected: a static writer on `CodeGen.EventAccessors` (it would
take the converter, the lowering and the annotation switch as arguments, a drawer of functions, principle 11), and a
copy per emitter (the defect this change fixes is precisely a second emitter without the first one's writer).

### The `[ServerOnly]` check and `inAComponent`

The class path calls `ComponentParser.StaysOnTheServer(e, inAComponent: false, model)`. For an
`EventDeclarationSyntax` that switch falls to `_ => false` whatever `inAComponent` is (read from the code), and
`[ServerOnly]` cannot be written on an event: measured with Roslyn 5.0.0 over the attribute's own `AttributeUsage`
(`Method | Class | Struct`), `[ServerOnly] public event Action E1 { … }` is CS0592, "not valid on this declaration
type". So no `inAComponent` value, `true` for the component included, changes an answer, and the writer drops the
call rather than pass an argument that decides nothing. The doc comment's "A `[ServerOnly]` event never crosses" goes
with it: it describes a declaration C# refuses.

## Risks / Trade-offs

- A record, a struct or a component that declares an event with accessors gains two methods; its transpiled pin
  moves by them and by nothing else. A pin that moves elsewhere is a defect of the move.
- A class's twin must stay byte-identical: the class path's output is pinned and the move is a relocation.

## Open

- `[ServerOnly]` on an ACCESSOR compiles (an accessor is a `Method` target, measured: no error for
  `[ServerOnly] add => …`). `StaysOnTheServer` has no `AccessorDeclarationSyntax` case, so such an accessor is
  written into every twin, as on the class path today. A follow-up issue under #849, not this fix.
- A field-like event on a component: not measured.
