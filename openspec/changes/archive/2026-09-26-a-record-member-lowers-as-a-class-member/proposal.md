# Proposal

Closes #432, a sub-issue of #164.

## Why

The record and struct emitter lowered a method with its own copy of the class emitter's lowering,
and the copy had been taught none of what the class path handles. An `async` method wrote an `await`
outside an async function, and an iterator a `yield` outside a generator: either one is a
SyntaxError, so the whole module failed to load, a record's own method and a default an interface
supplies alike. An `out` or `ref` parameter stayed in the signature and never came back. An
expression body read the variable its pattern binds before anything declared it, so
`o is SE m && m.V == V` threw `ReferenceError: m is not defined`, and `List<SE>.Remove`, which
delegates to the twin's `equals`, threw with it. Operators, conversions and computed properties
lowered their bodies with the same kind of copy. And a parameter was declared by its camel case,
where every reference in the body names it by its JavaScript identifier: `package` was declared as
itself, which no module parses.

Before this change merged, #484 and #399 fixed the last two in the copy, the pattern variables and
the reserved parameter names, and this change takes both from main. What it still fixes is the rest:
against main at a4112476, every async, iterator, out and ref case fails, and so does a getter that
yields.

## What Changes

- **One lowering for every type with methods** (`MethodLowering`): a method's signature and body,
  and a member's body, are built in one place that the class emitter and the record and struct
  emitter both call. A record's or a struct's method, operator, conversion and computed property go
  through it, so each shape the class path learns reaches them too.
- **Whether a method is async is asked of its return type's symbol** (found in review), in a class,
  a component, a record and an extension member alike: the name alone made a method returning a
  type called `TaskItem` async, and its callers read a Promise.
- **An accessor's block is lowered as a method's body**, so a getter that yields fills its buffer
  and a block declares the locals its `out var` binds (on main first with #484), in a record, a
  class and a component (found in review), and **a C# 14 extension member** goes through the same
  method lowering, its receiver in front, where a third copy wrote `yield` outside a generator.

## Impact

- The compiler: `CodeGen/MethodLowering.cs` (new), `TypeScriptEmitter` and `RecordTypeEmitter`.
- No public signature moves. A class's members are written as they were, every transpiled pin in the
  compiler suite holds, and the shared components' twelve transpiled records move in whitespace alone.
