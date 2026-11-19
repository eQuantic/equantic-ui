# Design

## Context

Three issues under #1174, each diagnosed with failing tests already written
(`.claude/pipelines/1511-every-accessor-and-indexer-reaches-its-twin/diagnose-151{1,2,3}.md`):

- `DefaultInterfaceMembers.Of` keeps only properties and ordinary methods
  (`member is not (IPropertySymbol or IMethodSymbol { MethodKind: Ordinary })`), so a default event
  never reaches an emitter, and both emitters' switches have no event arm.
- The registry keeps any public instance method carrying `[ServerAction]`, a special-name accessor
  included; the compiler honours `[ServerOnly]` only on an event's accessor and reads `[ServerAction]`
  on a component's event accessor as `[ServerOnly]`; `ServerOnlyContracts` reads only methods.
- `TypeScriptEmitter.Emit(ComponentDefinition)` writes the properties, the ordered statics and the
  indexers inside the branch of a concrete component (or an abstract one its condition counts), and the
  primitive branch never writes them.

Flutter has no row for any of this in `docs/FLUTTER-PARITY.md`: the question is about none of its
three trees, but about the compiler's twin of a C# type and the server's dispatch, which Dart, compiled
to one target, does not have. The answer is .NET's, measured where the design relies on it.

## Measured

`dotnet fsi` with Roslyn 5.9.0 (`LanguageVersion.Preview`) and reflection over the emitted assembly,
the script in the session's scratchpad:

| Question | Answer |
|---|---|
| An interface's field-like event's `add` | `IsAbstract` true, `MethodKind.EventAdd` |
| `Signal.FindImplementationForInterfaceMember(IHasFired.Fired.AddMethod)` | `Signal.Fired.add`, carrying `ServerOnlyAttribute` |
| `Derived.Fired.AddMethod.OverriddenMethod` over a `virtual` base event | `Base.Fired.add`, `IsOverride` true; the base's `IsVirtual` true |
| `Rung.FindImplementationForInterfaceMember(IBell.Rang)` for a default event | the interface's `IBell.Rang`, `IsAbstract` false, not static |
| `class Both : IA, IB`, each with a default `event Action E` | compiles; each interface's `E` is taken |
| `class Ringing : IChime { string Rang() }` beside a default `event Rang` | compiles |
| `[ServerAction]` on `add`, on `get` of a property and of an indexer, reflected | `add_Fired`, `get_Label`, `get_Item`, each `IsSpecialName` true; `Load()` false |
| `[Authorize]` (`Class \| Method`) on a property | CS0592; on the getter it compiles and `get_Label` carries it |

So every new decision is answered by the symbol: `IsSpecialName` for the server, `MethodKind` and
`FindImplementationForInterfaceMember` and `OverriddenMethod` for the contracts, which hold for
accessors as they do for methods. `FulfilledOnTheServer`'s entry test
(`IsVirtual || IsAbstract || IsOverride`) already admits an interface's and a virtual event's accessor.

## Decisions

### 1. A default event: `Of` admits it, the one writer writes it

`DefaultInterfaceMembers.Of` admits an `IEventSymbol` (never its accessor methods), on both of its
walks: the interface members and the private helpers a default reaches. Its other rules hold unchanged:
an interface's field-like event is abstract and stays out, and a more specific default wins. The event's
declaration (`EventDeclarationSyntax`) is written by `MethodLowering.EventAccessors(e, …, asStatic:
false)` under `InFileOf(e, …)`, the writer #1445 made the only one; it names the pair from the event's
`MemberTwinName()`, the name the subscription already hands `$eq.addToEvent`. An accessor marked
`[ServerOnly]` inside a default stays out of the twin, as on a class's own event, and a subscription
through the interface is refused by the direct path, since the bound accessor's own declaration carries
the mark.

A default event of an interface compiled into a referenced assembly has no body to write. The runtime
carries no vocabulary interface with a default event (none in Primitives, Components, Code or Sheet), and
`Delegation` would write it as a getter, which is wrong. So `Delegation` takes methods and properties
alone, and an event goes to the refusal of a default nothing can supply (EQ1008), whichever assembly
it comes from.

### 2. The two default switches become one dispatch

Two designs:

- **A. An event arm in each switch.** Four lines in each emitter. It costs nothing now, and leaves the
  dispatch written twice: the class's switch admits a property `when` its accessors have bodies, the
  record's `when ComputedGetter(…) is not null || IsSetterOnly(…)`; the class delegates through
  `EmitDelegatedDefault`, the record composes the same text inline; the record's arm carries a comment
  about a `[ServerOnly]` guard the class's never had. They have already drifted, and the next kind would
  be added to one.
- **B. One dispatch, each emitter its writers.** An abstract `DefaultMemberWriter` (in `CodeGen/`) owns
  the loop over `Of`, the refusal of a default that reaches an interface's static (EQ1008 `Homeless`),
  the `InFileOf`, the choice of the arm, the delegation of a vocabulary default and the refusal of what
  nothing can supply (EQ1008 `Unreadable`). Its abstract members are one per kind the language has, a
  method, a property, an indexer and an event, plus the delegated member, and each emitter supplies them
  with the writers it already has for its own members (`EmitClassMethod`/`EmitGetter`/`EmitSetter`/
  `EmitIndexer` and `Lowering.EventAccessors` for a class and a component; `EmitMethod`/
  `ComputedProperty`/`_lowering.Indexer`/`_lowering.EventAccessors` for a record and a struct). A kind
  added to the base is a compile error in each emitter until it is written: the instrument that fails.

**Chosen: B.** It is the structural fix principle 9 asks for, and principle 11 says shared behaviour is
a base class. The bodies the two emitters write stay theirs, so no output moves: a record still writes a
default as a record writes its own member, and each keeps its own writer of a delegated default (the
class's `EmitDelegatedDefault`, the record's inline text, moved into its writer as it is). The guard on
a property's arm is dropped from both: `Of` never returns an abstract implementation, so a default
property always has a body, and the guard could only send one to the wrong refusal.

### 3. EQ1007: a default event takes the names of its accessors

The twin holds a default event as `$add$<e>` and `$remove$<e>`, never as `<e>`. So in `OverloadedMethods`
an event among the defaults (`Of`'s, the class's and its bases') claims those two names, named by
`EventAccessors.Name`, and not `MemberTwinName()`: a class's method `Rang()` beside a default
`event Rang` is no clash (`AClassMethodOnADefaultEventsNameIsNoClash`), while two default events on one
name, from two interfaces, are refused, since `addToEvent` would reach one of them for both.
`InstanceMembers` still claims `<e>` for a class's OWN accessor event; that is outside this change
(an accessor event's own name is held by nothing on its twin either), and is recorded as a finding.

### 4. An accessor is never a Server Action (H1)

`ServerActionRegistry.ScanAssembly` and `AppSurface.HasActions` keep a method only when
`!method.IsSpecialName`, two predicates, no shared helper for two sites. An action is an RPC stub written
from a method's signature: an accessor cannot be one (an event's parameter is a delegate no JSON binds,
and a getter cannot await a call), so the server does not read the mark there, whatever the compiler
says. The registry fix stands on its own, so an assembly built by any compiler registers no accessor.

For the gate, as a fact: today `<Component>/get_Label` is registered and its body runs for any caller
(measured by the diagnosis, the middleware's counter went to 1). `[Authorize]` cannot be written on the
property (CS0592, measured). Written on the getter, reflection gives `get_Label` the attribute
(measured), and `ServerActionAuthorizationService.MetadataOf` reads the method's attributes and its
component's (read, not run), so an `[Authorize]` on the getter or on the component would be asked before
the body. Unmarked, nothing is asked. After this change the id is not found either way.

### 5. EQ2016, a server mark on an accessor (H2)

A new code, not EQ2012 widened: EQ2012 is an action nothing can address on an abstract component, and
`[ServerOnly]` on a property's accessor is no action at all. EQ2016 is free (`docs/DIAGNOSTICS.md`
documents up to EQ2015 and nothing raises 2016). Its two cases, in every type eqc writes:

- `[ServerAction]` on any accessor, of an event, a property or an indexer: the mark makes no action
  (decision 4), and no twin can call one from an accessor;
- `[ServerOnly]` on an accessor that is not an event's: honouring it would mean refusing every client
  read or write of the property (member access, initializers, `with`, patterns, compound assignment,
  deconstruction). That is a feature, not this fix; refused, the developer writes a `[ServerOnly]`
  method and is told so.

`[ServerOnly]` on an event's accessor stays what #1445 made it. The check is a declaration check run
beside EQ1007's in `ComponentCompiler` (`DeclarationOf(component)`, every kind, before any emitter), in a
class of its own imitating `OverloadedMethods.Check`, asking each accessor's SYMBOL through
`PlainClassModule.IsServerAction`/`IsServerOnly` (alias-safe, #1140/#1148). The message names the type,
the member and the accessor, and what to write instead. H3d was falsified by the diagnosis (CS0657: a
partial event's defining half cannot carry `[method: …]`), so the parser's early return on an accessor
with no attribute list is sound and stays.

With `[ServerAction]` refused on accessors, the component case leaves the accessor rule entirely:
`StaysOnTheServer`'s accessor arm and `IsServerOnlyAccessor` lose their `inAComponent` reading,
`MethodLowering.EventAccessors` its `IsUiComponent()` and `ReportIfAccessorStaysOnTheServer` its own, and
their messages say `[ServerOnly]` alone. That dissolves H3c (four answers to "is this a component" on the
accessor path) without reconciling them. Two #1445 rows move to the refusal:
`EventAccessorConformanceTests.KeptAccessors`' `Meter.Served` (`[ServerAction] remove`), whose module now
fails to build, so the row leaves `AnEventsServerOnlyAccessor_StaysOnTheServer` and its case becomes an
EQ2016 row; and the third `InlineData` of `ASubscriptionToAServerOnlyAccessor_IsRefused`, whose
`[ServerAction] add` is now EQ2016 at the declaration, not EQ2010 at the subscription.

### 6. A subscription through a contract (H3a)

The method case is solved (`ServerOnlyContracts`, asked by `ReportIfFulfilledOnTheServer`); accessors
join it:

- `ServerOnlyContracts.ServerOnlyMethods` yields the server-only accessors of events too
  (`AccessorDeclarationSyntax` under an `EventDeclarationSyntax`, by the same `StaysOnTheServer` rule);
- the interface loop admits `MethodKind.EventAdd` and `EventRemove` beside `Ordinary`,
  `UserDefinedOperator` and `Conversion`; the override walk needs nothing (`OverriddenMethod` answers
  for an accessor, measured);
- `ReportIfAccessorStaysOnTheServer` asks the contract question when the direct one is silent, with the
  subscription's receiver type (the `Left` of the assignment's member access), and EQ2010's message names
  the accessor that keeps the body.

So `s.Fired += a` on an `IHasFired` that `Signal` or `Hidden` fulfils with a `[ServerOnly] add`, and
`b.Fired += a` on a `Base` whose `Derived` overrides with one, are refused; `s.Fired -= a` (the remove
crosses) and a `Sibling : Base` build. The build-wide scan is the same one methods pay for: no new walk.

### 7. Every component path writes its twin members (#1513)

The three writers (`EmitComponentProperties`, `TypeInitializer.Members` when `orderedStatics`, and
`EmitIndexer` over `TwinMembers()`) move out of the concrete branch to just before
`if (component.IsPrimitive)`, beside the hydration map. Nothing they read is set inside the branch
(`slots`, `initializedStatics`, `orderedStatics` and the current class are set above), and the class
writer keeps insertion order, so a concrete component writes the same members in the same order: no
pinned module moves (the diagnosis found no pinned component with an indexer, and properties and statics
keep their place). The abstract base the branch's condition skips and the primitive branch now get them
too. The condition at the branch is not widened: it has lost members three times, and a counting list is
the patch this repository rules out. `orderedStatics` is already false for a primitive, and
`EmitComponentProperties` writes no pure instance auto-property, so a primitive's `Object.assign` is not
clobbered.

## Risks and trade-offs

- **The primitive's constructor.** A primitive's instance state is still not initialized by its twin's
  constructor: its instance field initializers are dropped (filed separately, under #1174), and so a
  `field`-backed property's store starts `undefined` there. The conformance rows avoid both, so each fails
  for one reason.
- **A primitive's `Render()`** override is not taken (filed separately); no row here calls `render`.
- **EQ2016 is a break** for an app that marked an accessor; in preview it is the cheapest form, and the
  message carries the migration.
- **The dispatch refactor** touches the record emitter's string path. The pins and the record suites are
  the net: a whitespace or order change in a record's default is a defect of the refactor.

## Open

- A class's own accessor event claims `<e>` in EQ1007's `InstanceMembers` where its twin holds
  `$add$<e>`/`$remove$<e>` (decision 3). Not chased; a finding for #1174.
- `[method: ServerOnly]` on a field-like event marks its compiler-written accessors and nothing refuses a
  client subscription (measured by the diagnosis); the accessor has no body, so it is a question of
  intent, left as the diagnosis left it.
