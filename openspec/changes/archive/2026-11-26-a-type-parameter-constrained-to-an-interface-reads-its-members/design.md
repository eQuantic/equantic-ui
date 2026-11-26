# Design

## How Flutter solves it

Dart has no separate interface declaration: every class is an implicit interface, and a type parameter's bound
(`T extends Shape`) is checked against it like any other type. There is no erased constraint to restore. The closest
TypeScript has is the same: an `interface` declaration a bound can name. So the twin is an `interface`, and the bound
names it, as `T extends ShapeBase` names a class since #1854.

## Which interface gets a twin

Every in-source interface whose twin can be written: the app's, a referenced project's, and the transpiled library's.
A twin only for the interfaces a constraint names was considered, and refused: a referenced project is compiled apart from
the app that constrains a class to its interface, so its build cannot see that constraint, and the app's module would
import a twin nobody wrote. An unused twin costs an app nothing, since only the modules a written module imports are
bundled.

The library's barrel re-exports each of its twins with `export type`, which `CompilationResult.IsInterfaceTwin` tells it
to. A plain re-export kept the twin's value reachable from the runtime's entry, and the served runtime carried seven
`var X=void 0` and their names. Re-exported as a type, no value of a twin is reachable and the bundler drops its module:
measured, the served runtime is byte for byte main's.

It can be written when the interface declares no type parameter and stands in no generic type, each member is an
instance property or method with no body, no member is static, an event or an indexer, it extends no
interface, and it is neither marked `[ServerOnly]` nor inside a type that is. An interface that extends another is fenced
rather than written with an `extends`: its base's twin would have to be found in another file, which the syntax-only
resolver cannot do, and the parser and the resolver must agree. Each fence leaves the constraint unconstrained,
as every constraint was before #1854.

## The module

The twin is written by the class module's writer, so its imports come from the same rule every class module's do: what
the emitted text names. It holds `export interface` with each member under its twin's name (`Area` is `area`, as the
class twin that implements it names it, and a method through `TwinMethodName`), its type as an annotation writes it, and
`readonly` where the property has no setter. Beside it, `export const IShaped = undefined;`,
the device a union's alias module uses: a module importing the name with a plain `import` finds a value whether or not
the bundler elides a type-only import, with `isolatedModules` on. Plain JavaScript writes the value alone.

## What does not move

A value typed as the interface stays `any`. Typing it would move every module that holds one, and the members a value of
the interface reads are already accepted. That is a separate change, if it is ever wanted.
