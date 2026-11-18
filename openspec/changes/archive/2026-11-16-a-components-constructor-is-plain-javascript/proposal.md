# Proposal

Closes #1574, a Bug under #1174.

## Why

The twin of a component that takes its parameters through its constructor was written with
TypeScript annotations in a plain JavaScript module: `tag?: any` for a parameter without a default and
`size: any = 2` for one with a default. Bun refuses the module ("Expected ) but found ?"), so a module
written without annotations, as the design host and the conformance harness ask, failed on a component
that compiles in C#. The issue was found with a constructor ending in `params`, but any parameter of a
component's constructor wrote it: the emitter spelled the twin's constructor by hand instead of through
the signature writers every other hand-written signature goes through.

## What Changes

- A component's twin writes its constructor parameters through the same writers as every other
  signature, so plain JavaScript gets `constructor(tag, size = 2, items, props)` and TypeScript keeps
  `constructor(tag?: any, size: any = 2, items?: any, props?: any)`.

## Impact

- `src/eQuantic.UI.Compiler/CodeGen/TypeScriptEmitter.cs`. No API changes, and an app writes nothing new.
