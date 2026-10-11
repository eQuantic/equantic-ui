# Design

## Context

eqc writes one module per type, named by the type, and a module that names another imports it as
`import { X } from "./X"`. The name was the type's simple C# name everywhere: the parser's
definitions, the dependency resolver's set of modules, the emitters' class declarations, and every
strategy that writes a type into code (`new`, `instanceof`, a static member, an operator, a zero, an
annotation, a hydration map). A nested type broke the scheme three ways: a plain class or a static
class nested outside a component had no module, a component's nested static classes were written into
the component's module unexported, and a nested record or struct had a module named like a top-level
type, which could write the same file.

## Goals / Non-Goals

**Goals:**

- One name for a type's twin, read by everything that writes or resolves one.
- A nested type is a module like any other, so the import machinery that exists serves it unchanged.

**Non-Goals:**

- A nested enum or interface: an enum lowers to its values and an interface to nothing.
- Reflection's names (`GetType().Name`, `nameof`), which stay the C# names.

## Decisions

### `Owner$Nested`, a module of its own

The twin of a type declared inside another is named by the chain of the types that contain it and its
own name joined by `$`: `Cart$Item`, `A$B$C`, generic arguments erased (`Box<T>.Node` is `Box$Node`).
C# allows no `$` in a name, so the twin can never meet a top-level type's, and its module is named
the same. Every reference writes that name and the module that holds the reference imports it from
`./Cart$Item`, through the machinery a top-level type already takes.

Alternative: write the nested types into their owner's module, as a component's nested static
classes were. Rejected: every emitter would need to merge another type's code and imports into its
own, a reference from outside the owner would need the owner's module to re-export it, and the four
emitters would each carry a second shape of module. Alternative: the owner's static property
(`Cart.Item`). Rejected: TypeScript cannot read a value as a type, so every annotation of a nested
type would be `any`, and the owner's module would have to be loaded before its nested type's.

### One name, read everywhere

A type symbol's twin name (`TwinName`) and a declaration's (the same walk over its parents, for a
host with no model) replace the simple name in the parser, the dependency resolver's scan, the
emitters' class declarations and the strategies that write a type. A record's text keeps the C# name
it prints, as .NET's `PrintMembers` writes it, and so do the diagnostics, which speak C#.

How Flutter answers it: Dart has no nested classes, so a library writes each class at its top level
under a name of its own, and a private one takes a leading `_`. The `$` is the same idea in a name C#
cannot spell.

### Who has a twin

A nested type has a twin where a top-level type of its kind would, and where its owner crosses: a
nested type of a `[ServerOnly]` owner, an exception or an attribute has none, as its owner has none,
whatever it is itself. A component's nested static class is a static helper's module like any other.

## Risks / Trade-offs

- [A module file whose name holds a `$`] → the bundler, the server's module route and every host's
  file system take it; the conformance suite runs the bundled graph, and the dashboard sample serves
  it.
- [A hand-written runtime file naming `CodeMetrics`] → none: only the transpiled `CodeBlock` reads it,
  and it regenerates.
