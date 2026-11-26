# Proposal

Closes #1878, a Bug under #1587 (The transpiler's fences hold on every path, part five), the family of #1854.

## Why

Since #1854 a type parameter constrained to a class with a twin is declared `T extends` that class, so a member read on a
`T` type-checks. An interface has no twin, so a type parameter constrained to one stayed unconstrained, and a member read
on it, the most common shape of a constrained generic in C#, is refused by the runtime's `tsc` (TS2339). A value typed as
the interface is declared `any` and reads anything, so the `T` standing for it was stricter than the interface itself. A
generic class of this shape cannot be part of the runtime-provided library, and an app's module carries the same errors
the day its types are checked.

TypeScript has no generic `any`: since TS 3.9, `T extends any` no longer lets a member be read. Writing the constraint as
`Record<string, any>` reads members but fails the constraint as soon as a struct or a primitive type argument meets it.

## What Changes

- eqc writes a twin for every in-source interface whose twin can be written: a module holding `export interface IShaped
  { readonly area: number; describe(unit: string): string; }` and `export const IShaped = undefined;`, as a union's alias
  module holds, so a plain import resolves in JavaScript. Plain JavaScript writes the value alone.
- A type parameter constrained to such an interface is declared `T extends IShaped`, the interface imported, in
  TypeScript only.
- Named fences, where an interface gets no twin and its constraint stays unconstrained: a generic interface or one in a
  generic type, an interface with a static member, a default member, an event or an indexer, one extending an interface
  with no twin (a BCL one), and one the build keeps on the server.
- A value typed as an interface stays `any`: only constraints name the twin, so no other module's types move.

Nothing an app writes moves. The runtime library gains the twins of seven of its own interfaces, the code editor's six
providers and the gesture arena's member, empty in JavaScript and exported from its barrel. No developer surface moves;
the compiler's new members are declared in the branch's own `PublicAPI.Unshipped` file.

The rules followed: the structural fix over the patch (a twin the constraint can name, not a looser constraint), one
writer per level (the twin goes through the class module's writer and its imports), and the vocabulary speaks no
target's language (the twin carries the members' twin names, as every class twin does).

## Impact

- `src/eQuantic.UI.Compiler`: the parser takes an interface whose twin can be written as a definition; the class
  module's writer writes its twin; the dependency resolver counts it as a module; the constraint namer names it
- The runtime library: a twin module for each eligible interface of `eQuantic.UI.Code`, exported from the barrel
- The emission shapes the runtime's `tsc` reads, and the compiler's tests of each fence
- `openspec/specs/transpiler-classes`: the requirement that a class's TypeScript twin passes the runtime's type check
- An entry under `docs/ledger/`
