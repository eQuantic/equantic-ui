# Design

## Context

See proposal.md, Why, for the defects. The constraints that shape the approach, measured on main at
d09f7bec:

- **Who writes what, and when.** The C# compiler runs first, with the source generators inside it
  (`eQuantic.UI.Generators`, netstandard2.0, Roslyn 4.14). eqc runs after it (`CompileEQuanticUI`,
  `BeforeTargets="Build"`), reads the generated sources from disk (`EmitCompilerGeneratedFiles` is
  load-bearing in `Sdk.props` for exactly that), and writes the twins and the bundle. eqc cannot add
  anything to the app's assembly, which is already built when it runs.
- **The server half today.** `ServerRenderingService.Snapshot` walks `ComponentExpansionScope.FieldsOf`,
  renames `<Name>k__BackingField` to `Name` and nothing else, skips a field whose type is an interface
  outside `System` (`IsDependency`), and serializes the rest whole with `EqJson.Options`, whose naming
  policy does not touch dictionary keys. The same snapshot feeds the page render and the navigation
  payload. The server-side restore between discovery rounds uses a different form (`Capture`, keyed by
  the CLR field name) and is not part of the wire.
- **The browser half today.** eqc names a member by the one rule it uses everywhere (`ToCamelCase`), and
  writes `static $hydration` with the wire spec of each field whose JSON form differs from its runtime
  type. The runtime (`applyServerFields`) adopts a key only when the instance already holds it as its own
  property or a declared accessor. A twin assigns a captured constructor parameter only when an argument
  arrived (`if (identity !== undefined) this.identity = identity`), so a page the router builds without
  arguments never holds that property and refuses the key.
- **Who builds a component from the container.** Only the page of a request
  (`ServerRenderingService.cs:124`, a `[Page]`, the not-found and error pages included since they are
  `[Page("/404")]` and `[Page("/500")]`) and the target of a server action, which never renders. Every
  other component is built by its parent's `Build`, on both sides, with the same arguments.
- **What is in the payload.** Only the components that prefetched. A page that reads a service in its
  `Build` without prefetching carries nothing today, so the browser draws the other branch silently.

## Goals / Non-Goals

**Goals:**

- One description of what crosses, written once by the build and read by both halves, so the two
  cannot disagree.
- The description derived from what the browser-side code reads, so a server value crosses as exactly
  that and nothing more.
- A refusal at build time, never a divergence in the browser, where the description cannot be made.

**Non-Goals:**

- Projecting prefetched data (it still crosses whole, as the wiki's "Every field is public" says), and
  members marked as never crossing: slice S3.
- Evaluating a call on a server value on the server, and non-deterministic reads in a `Build`: slice S4.
- Photon: a native host renders locally and carries nothing across a wire.

## Decisions

### D1. How Flutter solves it: it does not have the problem

Flutter runs one program in one process, so there is no server render, no payload and no hydration.
`docs/FLUTTER-PARITY.md` records this boundary as the disruptive difference (the `FutureBuilder` /
`StreamBuilder` row: we fetch on the server and hydrate the answer). The frameworks that do have it
converge on the shape chosen here: React Server Components refuse server-only modules in client code at
build time and taint objects that must not cross; Qwik serializes only the state the client code
captures; Relay hands each component only the fields it declared. Blazor's `PersistentComponentState`
is the explicit form (the developer opts each value in), which the product principle rules out as the
primary path: the SDK knows what the browser reads, so it decides.

### D2. The source generator owns the description, as assembly attributes

The generator analyses each component and writes a **hydration manifest** into the generated sources:
one assembly attribute per crossing member, naming the component type, the member as C# declares it
(a field, a property, or a captured primary-constructor parameter), and its projection when it has
one. The wire name is not recorded: it is the rule of D3, applied by every reader. The types of those attributes live in `eQuantic.UI.Primitives`, beside the other
contract attributes.

- **The server** reads the attributes of the assemblies it scans, once, at startup.
- **eqc** reads the same attributes from the generated sources it already reads, and emits each twin's
  adoption from them.

This is the idiom the SDK already uses for a declaration that two later stages read
(`PhotonProgramGenerator` writes `[assembly: PhotonCapability(...)]`, and the build and the shell read
it). Alternatives considered:

- *eqc writes a manifest file the server loads at run time.* eqc runs after the assembly is built, so the
  manifest would live beside the app instead of inside it: a file a publish can drop, and that a test
  host compiling a component without eqc never has.
- *The analysis compiled twice, into eqc and the server, from `src/Shared`.* Two runs of one analysis
  over two compilations can disagree, which is the defect this change removes.

### D3. A wire name is the twin's member name, from one rule

The wire name is the name the twin declares the member under. That rule (`ToCamelCase` on the C# name)
moves to `src/Shared` as `TwinName` and is compiled into the generator and the server, and eqc's own
naming calls the same function, so there is one owner of it. The server applies it to a top-level
member and to every member of a projection alike, which is why the manifest records no name. The runtime then adopts the keys the twin's hydration map declares, whether
or not the instance already holds the property, since the map, not the constructor, says what the twin
declares. That is what lets a captured parameter the router did not pass be adopted.

### D4. What is a server-only value

A value is server-only when it comes from the container: a constructor parameter of a `[Page]` whose
type is a class (an interface from outside `System` stays a dependency the browser resolves, as today),
and a field or property that a constructor body or an initializer assigns from one. A non-page
component's parameters are values its parent passes on both sides, and are data.

The server keeps a second line of defence that the build cannot provide: a value whose run-time type the
container registers as a service (`IServiceProviderIsService`) is never written whole, whatever the
manifest says (a field typed `object` is the case the build cannot see).

### D5. The projection is the set of paths the browser-side code reads

The analysis walks the bound tree (`IOperation`) of every member the twin emits, which excludes
`[ServerOnly]` members and `[ServerAction]` bodies, following each server-only value:

- A null test (`is null`, `== null`, `?.`) records presence.
- A member read records the member and follows its value, until the value is a leaf (a primitive,
  `string`, an enum, a date or time type) or is used whole as a leaf argument.
- Passing the value to an in-source component's constructor composes with that component's own
  analysis of the parameter, so a child reading `identity.DisplayName` adds `DisplayName` to the page's
  projection.
- Anything else stops the analysis and fails the build with EQ2114 at that expression: a method call on
  the value (S4 lifts the ones without browser-dependent arguments), passing it to a method or a
  framework component whose source the build does not have, storing it in a collection, comparing it by
  reference, enumerating it, or converting it to text.

What crosses is a plain object with exactly those members, each holding the value the server's object
answered (a computed getter crosses as its result), or null. The twin's hydration map marks the member
as a projection, and the runtime adopts it as plain data and never rebuilds a class twin from it: a
projection lacks the members a twin's getters would compute from, the failure a `Rect` crossing with
its computed getters once showed in the other direction.

### D6. The server writes from the manifest and nothing else

Per component type the server resolves each manifest entry once to an accessor: a field, a property
(read through the property, so no synthesized name is guessed), or a captured parameter, whose storage is
the field the C# compiler synthesizes as `<name>P`. That convention is the only one the server keeps, in
one place, pinned by a test that compiles a real primary constructor and fails if the compiler ever
names it differently. A projection is evaluated by walking its paths on the live object after the
render.

A component enters the payload when its manifest has an entry: it prefetched, or it carries a
projection. A type the manifest does not describe carries nothing, and the server logs it. There is no
fallback to reflection: a second path beside the described one is the shape this change removes.

### D7. EQ2114 is reported by the generator

The generator reports EQ2114 as a compiler error, so it shows in the editor as the page is written and
fails `dotnet build` before eqc runs. It belongs to the client/server boundary family in
`docs/DIAGNOSTICS.md` (EQ2102–EQ2107, EQ2112), next to the APIs a client component may not reach. The
message names the page, the value, the expression where the analysis stopped, and the two ways out.

## Risks / Trade-offs

- [A read the analysis misses leaves a member out of a projection, and the browser reads undefined] →
  For a server-only value every unrecognized use is EQ2114, not a guess; data still crosses whole in
  this change; and a cross-boundary test renders real pages on the server and runs their twins on the
  payload the server wrote.
- [Every app build now depends on the generator for its state to cross] → It already does for the
  factory surface, and the SDK adds the analyzer itself. A test project that compiles components without
  the SDK adds the generator as an analyzer reference.
- [EQ2114 fails builds that rendered before] → They rendered with the whole service in the HTML, and the
  browser could only agree with the server through that leak. The migration line is in the proposal and
  goes in the wiki's Upgrading page for the release.
- [The analysis runs in the editor on every keystroke] → It is incremental per component declaration
  and walks only the members a twin emits; pages are a handful in any app.
- [The wire keys change] → Only a hand-written reader of `__INITIAL_STATE__` would notice, and nothing
  the SDK documents reads it.

## Migration Plan

Nothing for an app beyond EQ2114. The wiki's ServerIntegration page loses "Store results in FIELDS ...
A property does not cross", and its "Every field is public" section says what a service crosses as. The
Upgrading entry is written when the release is cut.
