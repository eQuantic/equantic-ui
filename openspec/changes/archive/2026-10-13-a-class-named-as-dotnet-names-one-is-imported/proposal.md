# Proposal

Closes #888, a Bug under #849.

## Why

A class the app or a project it references declares under a name .NET uses for one of its own types
reached the page with no module and no import. A library's `static class Type` was written into the
page's module as `Type.family()`, the build said nothing, and the browser met a name nothing defined.
A library's own exception class met the same: `new OrderException('x').code`, and no import of it.

The emitter keeps a list of names it takes for .NET's, which map to JavaScript or to the runtime,
and dropped the import of any name on it before it asked whether the build writes a module of that
name.

## What Changes

- A name of the list is the app's where the module's own code names a type of the app, or of a
  project it references, by it. A class called `Type`, `Uri`, `Console`, `Convert`, `Math` or `Regex`
  is imported by the file that means it, and so is an exception class one of them declares.
- A file that calls `System.Math` beside an app that declares its own `Math` still imports nothing
  of that name.

For a developer who uses the SDK: a class may be called what .NET calls one of its own.

## Capabilities

### New Capabilities

- `module-imports`: which sibling modules a written module imports.

### Modified Capabilities

None.
