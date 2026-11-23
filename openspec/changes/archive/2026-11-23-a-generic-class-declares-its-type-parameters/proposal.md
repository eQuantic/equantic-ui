## Why

Closes #1845, a Bug under #1587, found by #1563 (a gesture of the app's own is recognized), which it blocks.

A plain class that is generic was written as a module whose class declared no type parameters, while its members still
named them. `GestureRecognizerFactoryWithHandlers<T>` transpiled to `class GestureRecognizerFactoryWithHandlers` with
`constructor(create: () => T, …)`, which the runtime's `tsc` refuses (`Cannot find name 'T'`), so a generic class could
not be part of the runtime-provided library. A generic record and a generic struct had the same gap, and a record's
members named its own type without its closure (`with(patch: any): Pair`), which `tsc` refuses too.

Only the component path wrote a class's type parameters, from `classDecl.TypeParameterList`. The plain and static class
writer took none, and the record emitter wrote its header by hand. And the class writer wrote the list whatever the
mode: a generic component compiled to plain JavaScript was `class Listing<TItem> {`, a parse error that costs the
module.

## What Changes

For a developer using the SDK: a generic class, record or struct of theirs, or of the library, is a module the runtime's
type check takes. Its JavaScript is unchanged, and a generic component compiled to plain JavaScript now parses.

- **One list.** `TwinTypeParameters` reads a declaration's type parameters, and every kind of class reads it: the
  component path, the plain and static class writer, and the record and struct header.
- **A record names its own type by its closure.** Its `with` and `$clone` answer `Pair<TKey>`, and its static `$zero`
  answers `Pair<any>`, since TypeScript forbids a static member to read a class's type parameters.
- **TypeScript only.** The class writer writes the list only in TypeScript, as it writes `abstract`.

Parts reached: eqc only (`ComponentParser`, `TypeScriptEmitter`, `RecordTypeEmitter`, `TypeScriptCodeBuilder`, the
syntax extensions). The compiler is a tool, and no contract surface or developer surface moves. The shared components
the runtime ships are unchanged byte for byte.

Rules of `docs/PRINCIPLES.md` followed: one writer per level. The type parameter list had one reader, the component
path's, and now has one home that every class writer reads.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: "A class's TypeScript twin passes the runtime's type check" covers a generic class, record and
  struct, and plain JavaScript.

## Impact

`tests/eQuantic.UI.Compiler.Tests` gains `GenericTypeParametersTests`, nine cases over both modes, five of which fail on
main. `SharedComponentTranspilationTests` gains a generic class, record and struct among its emission shapes, written to
`src/eQuantic.UI.Runtime/src/shared/__fixtures__/`, which the runtime's `tsc` refuses six times on main.
