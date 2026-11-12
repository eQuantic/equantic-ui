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
public IEnumerable<JsClassMember> EventAccessors(EventDeclarationSyntax e, Func<TypeSyntax?, string> declaredType,
    bool asStatic, bool inAComponent)
```

It yields `$add$<twin>` and `$remove$<twin>` (`CodeGen.EventAccessors.Name`, the twin name from
`IEventSymbol.MemberTwinName()`), `static` for a static event or a static class, each with its `Origin` on the
accessor, the body from `AccessorBody` or `ExpressionBody(…, returns: false)`, exactly what `EmitEventAccessors`
writes today. Callers:

- `EmitStaticMembers`: `c.Member(member, member.Origin?.Member)` for each, where the loop over
  `EventDeclarationSyntax` is now (`TypeScriptEmitter.cs` ~1903).
- `RecordTypeEmitter.EmitModule`: `sb.Append(Written(member))`, after the indexers, as they are written.
- `Emit(ComponentDefinition)`: over `component.ClassSyntax`, inside the `_builder.Class(…)` callback, with
  `asStatic: false`, AFTER the chain of branches rather than inside one: the primitive branch (an
  `HtmlElement`) and an abstract base holding only a field and an event, which takes no branch, are
  components too (the gate's round 1 measured the second: .NET 2, JavaScript threw).

Each loop reads the type's `TwinMembers()`, as the indexer loops beside them do.

`TypeScriptEmitter.EmitEventAccessors` is deleted. Rejected: a static writer on `CodeGen.EventAccessors` (it would
take the converter, the lowering and the annotation switch as arguments, a drawer of functions, principle 11), and a
copy per emitter (the defect this change fixes is precisely a second emitter without the first one's writer).

### An accessor that stays on the server

`[ServerOnly]` cannot mark an event: measured with Roslyn 5.0.0 over the attribute's own `AttributeUsage`
(`Method | Class | Struct`), `[ServerOnly] public event Action E1 { … }` is CS0592. It CAN mark an accessor, a
method target (`[ServerOnly] add => …` compiles), and so can `[ServerAction]`. Before this change only a class's
twin wrote an event's accessors, and it wrote such a body; writing the accessors into every twin would have taken
it to the browser on three more paths (gate round 1, D3). So the rule is decided where every other one of what
crosses is, `ComponentParser.StaysOnTheServer`, which gains a case for an event's accessor: it stays on the server
when it is marked `[ServerOnly]`, in any type, or `[ServerAction]` on a component's, the one type where that
attribute means a body that never crosses, exactly as for a method. The writer asks it of each accessor, and the
scans that walk a declaration (`RuntimeProvidedTypeScanner`, `NamedSourceTypes`) stop at it as they stop at a
`[ServerOnly]` method. That is why the writer takes `inAComponent` again: it decides a `[ServerAction]` accessor.

A subscription from client code that calls such an accessor is refused, EQ2010, as a call of a `[ServerOnly]`
method is: C# calls the accessor for the code without naming it, as it calls `Dispose` in a `using`, and the call
would reach a member the browser does not have (`HostOnlySymbolExtensions.ReportIfAccessorStaysOnTheServer`,
asked by `EventSubscriptionStrategy`). The accessor beside it, unmarked, crosses and runs.

## Risks / Trade-offs

- A record, a struct or a component that declares an event with accessors gains two methods; its transpiled pin
  moves by them and by nothing else. A pin that moves elsewhere is a defect of the move.
- A class's twin must stay byte-identical: the class path's output is pinned and the move is a relocation.

## Open

- A field-like event on a component: not measured.
- A subscription through an interface or a virtual event whose implementation's accessor stays on the server: the
  call binds the contract, whose accessor carries no mark, and the runtime composes the list where the twin holds
  no method. The contract fence (`ServerOnlyContracts`) knows methods only. A follow-up under #849.
- A `[ServerOnly]` accessor of a PROPERTY: `StaysOnTheServer`'s new case is an event's accessor alone, as the
  property writers do not ask it. A follow-up under #849.
- An interface's default event accessors are written into no implementing twin (F1, filed separately), and the
  server registers a component's `[ServerAction]` accessor as a callable action (F2, filed under #849).
