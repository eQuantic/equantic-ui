# Proposal

Closes #1511, #1512 and #1513, three sub-issues of #1174, found by the review of #1445's fix (an
event's accessors reach every twin). One delivery, because the three are the same defect seen from
three sides: a member C# gives a type that the type's twin, or the server, does not hold as C# does.

## Why

Three silent divergences, each measured by a test already in the tree:

- an interface's default event never reaches an implementing twin, so `t.Ticked += h` composes a
  list the default never reads: .NET answers `52|1`, the browser `0|0`, and the build says nothing
  (#1511);
- a mark on an accessor does what nobody asked: `[ServerAction] get` on a component's property is
  registered by the server and its body runs for any caller who names `<Component>/get_Label`, while
  the browser runs the same body in the twin; `[ServerOnly]` on a property's or an indexer's accessor
  crosses with its body; and a subscription through an interface or a virtual event reaches an
  accessor kept on the server where the build refused only a direct one (#1512);
- an abstract component base's indexer and ordered statics, and a primitive component's indexer and
  bodied properties, are written into no twin: `cells[1]` throws "item is not a function", a base's
  `static readonly int[] Seed` reads `undefined` (#1513).

## What Changes

- **A default event reaches every twin.** An interface's event with a default `add` and `remove` is
  written into the twin of every class, record, struct and component that takes it, through the one
  writer #1445 made (`MethodLowering.EventAccessors`), and a subscription through the interface runs it.
  Its twin names are `$add$<e>` and `$remove$<e>`, so a method `E()` of the class beside it is no clash,
  while two default events on one name are refused (EQ1007).
- **One dispatch for the defaults.** The emitters of a class and of a record or a struct each switched
  over the defaults a type takes, and the two switches had already drifted. They become one dispatch,
  whose writers each emitter supplies, so a kind of member cannot be written by one emitter and missed
  by the other.
- **An accessor is never a Server Action.** The server's registry and the app's surface skip a
  special-name method, so `[ServerAction]` on an accessor registers nothing and a call naming it is
  "not found". The build refuses the mark where it is written, with a new diagnostic, **EQ2016**, "a
  server mark on an accessor": `[ServerAction]` on any accessor, and `[ServerOnly]` on an accessor that
  is not an event's. **BREAKING**: an app that marked an accessor now fails to build. Migration line:
  move the code into a `[ServerAction]` (or `[ServerOnly]`) method and have the accessor call it from
  server code, or drop the mark.
- **A subscription through a contract meets the fence.** `s.Fired += h` bound to an interface's event,
  or to a virtual event, whose accessor some type that crosses keeps on the server (`[ServerOnly]`), is
  refused with EQ2010 where its receiver can be that type, as a call through a contract already is
  (#816).
- **Every component path writes what its type declares.** An abstract component base and a primitive
  component get their indexers, an abstract base its ordered statics, and a primitive its bodied and
  static properties, from the same writers a concrete component uses, at the position they hold today, so
  no pinned module moves.

For a developer using the SDK: the C# they already write runs as .NET runs it, and one mark they may
have written (`[ServerAction]` or `[ServerOnly]` on an accessor) now fails the build, naming the member
and what to write instead.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-interfaces`: a default event is one of the defaults a class keeps, and takes the names of
  its accessors in the name check.
- `transpiler-classes`: an event's accessor stays on the server only by `[ServerOnly]`; every component
  path writes its indexers, ordered statics and bodied properties.
- `client-boundary`: a subscription through a contract a server-only accessor fulfils is refused; a
  server mark on an accessor is refused where it is declared (EQ2016).
- `server-actions`: an accessor is never a Server Action.

## Impact

Parts reached:

- eqc: `DefaultInterfaceMembers`, the two emitters' default dispatch (`TypeScriptEmitter`,
  `RecordTypeEmitter`), `OverloadedMethods` (EQ1007), a declaration check run beside EQ1007's in
  `ComponentCompiler` (EQ2016), `ComponentParser.StaysOnTheServer`, `MethodLowering.EventAccessors`,
  `ServerOnlyContracts` and `HostOnlySymbolExtensions` (EQ2010), and the component path of
  `TypeScriptEmitter.Emit`;
- the server: `ServerActionRegistry.ScanAssembly` and `AppSurface.HasActions`;
- not reached: the runtime (its `addToEvent` already calls `$add$<e>` where a twin has it), the web
  realizer, Photon, the SDKs, the templates.

The public surface does not move: every member touched is internal or keeps its signature, and no
`PublicAPI.Unshipped.txt` changes. The developer surface does not move
(`developer-surface.baseline.txt` holds no diagnostic). `docs/DIAGNOSTICS.md` gains the EQ2016 row and
corrects EQ2010's, `diagnostics.baseline.txt` gains EQ2016's reporting site, and the wiki's Diagnostics
and SupportedFeatures pages change in their five languages.

Principles followed: 9 (the structural fix: one dispatch rather than a third arm in two switches; a
declaration refusal that fails rather than a mark silently honoured differently by each side; the
indexer moved out of a branch rather than a counter added to the branch's condition), 10 (every
decision asked of the symbol: `IsSpecialName`, `MethodKind`, `FindImplementationForInterfaceMember`,
`OverriddenMethod`), 13 (the `[ServerAction]`-on-a-component's-accessor reading #1445 introduced goes,
with nothing kept beside the refusal), 14 (each gap fixed in the SDK, none worked around in a test).
None is bent.
