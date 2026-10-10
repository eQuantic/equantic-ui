## Why

Two places where the browser shared or skipped what C# copies:

- A record that declares its own copy constructor (`protected R(R original)`) is copied by `with` through
  it in C#, and the twin's `with` copied every member onto the prototype instead. It never ran the
  constructor's body, copied members the constructor does not assign, and shared a deep copy the
  constructor writes (#589). Measured: `.NET "0|101|30"`, browser `"1|0|3"`.
- A mutable struct and a value tuple are copied on every assignment, argument, return and boxing in
  C#, and JavaScript hands the same object over, so a write through one name showed through every
  other (#560): `var u = t; t.Item1 = 9;` left `u.Item1` at 9.

## What Changes

- A record whose chain declares a copy constructor carries a copy step per level, and `with` copies
  through it: a synthesized level copies its own members after its base's step, and a declared one
  starts its members at their zero, runs its base's step with what `: base(…)` passes, then its body.
  No initializer runs, as in C#. A record whose chain declares none keeps its copy as it was.
- A write of a member of a mutable struct or a value tuple first gives the variable, parameter, field
  or array element that holds it a copy of it, each value along the path first (copy on write), and a
  method that writes the value's state is called on such a copy. `this` is copied where it leaves its
  struct's member. A mutating call on a property's or a call's result runs on a copy. Every mutable
  struct twin carries `$clone()`.

## Capabilities

### New Capabilities

### Modified Capabilities

- `transpiler-records`: `with` runs a record's own copy constructor, and a mutable struct or tuple is
  copied where C# copies it.

## Impact

- `RecordTypeEmitter` (the copy step, `$clone`), a new `ValueCopies` settled by the dispatcher beside
  `ValueFlow`.
- No public API change. The runtime's own structs are all readonly, so its twins do not change.
