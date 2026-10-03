# Proposal

Closes #509 and #510, sub-issues of #164 ("The transpiler's fences hold on every path").

## Why

A component that renders on the server carries its state to the browser in the page
(`__INITIAL_STATE__`), and the transpiled twin adopts it before its first build. The two halves of that
contract have two owners. The browser half belongs to eqc, which knows every member's name and type and
already writes the twin's `static $hydration`. The server half is guessed at run time by reflection:
`ServerRenderingService.Snapshot` names a member from the field the C# compiler synthesized, and
decides what crosses by the shape of its type ("anything but an interface outside `System`").

Every defect the equantic.tech pages found in this boundary is that drift, measured on main at d09f7bec:

- An auto-property never hydrates. The server writes the key `Downloads`, from
  `<Downloads>k__BackingField`, the twin declares `downloads`, and the runtime adopts a key only when the
  twin declares that exact member. The site works around it in every page with prefetched data
  ("Fields, not properties", `DocsPage.cs`).
- A captured primary-constructor parameter crosses as `<identity>P`, a name the twin never reads, so the
  browser draws the other branch of a decision the server drew (#509).
- A service registered as a class is serialized whole into the HTML, because its type is not an
  interface. The site leaked its identity provider's authority that way (#510). Fixing that alone
  would break, in the browser and only there, every page whose `Build` reads such a service.

The structural fix is one owner for both halves, deriving the contract from what the code that runs in
the browser actually reads, so the SDK resolves the boundary itself instead of asking the developer to
restructure a page around it.

## What Changes

- The source generator analyses each component's browser-side members (its `Build`, properties,
  handlers, and the members they call) and writes a **hydration manifest** as assembly attributes: which
  members cross, under which wire name, and for a server-only value, which of its members.
- The server writes the payload from the manifest and nothing else: no field name is guessed and no type
  shape decides what crosses. A type the manifest does not describe carries no state.
- eqc reads the same manifest from the generated sources and emits the twin's adoption from it, so both
  halves are written from one description.
- An auto-property and a captured primary-constructor parameter hydrate like a field. The
  "fields, not properties" rule is gone.
- A service a page receives from the container (a class-typed constructor parameter of a `[Page]`, or a
  member assigned from one) never crosses whole. What crosses is its **projection**: the members the
  browser-side code reads, and whether it is null. `identity is null` in a `Build` crosses as a presence,
  never as the object, and the browser draws the branch the server drew.
- **BREAKING:** a page whose browser-side code uses a server service in a way the analysis cannot follow
  (the value escapes into a method it cannot see, or a method on the service is called with an argument
  that changes in the browser) fails the build with **EQ2114**, naming the page, the member and where the
  analysis stopped. It rendered before, with the whole service in the HTML. Migration: decide on the
  server, in `PrefetchAsync` or a `[ServerOnly]` member, and keep the result in a field; or call a
  `[ServerAction]` when the browser's state is an input.
- The wire keys change to the twin's names (`Downloads` crosses as `downloads`). Nothing an app writes
  reads them.

Out of this change, as later slices with their own proposals: projecting prefetched DATA to what the
browser reads, with members marked server-only that never cross (S3); evaluating a call on a server value
on the server and carrying its result, and carrying the server's answer to a non-deterministic read such
as `DateTime.Now` in a `Build` (S4).

## Capabilities

### New Capabilities

- `hydration-contract`: what crosses from a server render to the browser, under which name, and in which
  shape, and what the build refuses because it cannot cross.

### Modified Capabilities

(none)

## Impact

- **eQuantic.UI.Generators**: a new analysis over the bound tree of each component's browser-side
  members, and the manifest it writes.
- **eQuantic.UI.Primitives**: the manifest's attribute types. The public surface grows; nothing is
  retired.
- **eQuantic.UI.Server**: `ServerRenderingService` reads the manifest at startup and writes each payload
  from it, for the page render and the navigation payload alike. The reflection naming and
  `IsDependency` go. As a second line of defence, a value whose type the container registers as a
  service is never written whole.
- **eqc (eQuantic.UI.Compiler)**: reads the manifest from the generated sources, emits the twin's
  adoption from it, and reports EQ2114.
- **The runtime**: a projection is adopted as plain data and never rebuilt into a class twin.
- **The SDK and the templates**: nothing an app writes changes. The developer surface does not move.
- **Documentation**: `docs/DIAGNOSTICS.md` (EQ2114), the wiki's ServerIntegration and Diagnostics pages
  in English and Portuguese, and `docs/LEDGER.md`.
