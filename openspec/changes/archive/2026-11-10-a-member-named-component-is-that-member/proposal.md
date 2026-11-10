## Why

Closes #1299, a Bug that blocks 1.0's critical path (#1127): the element engine's emission.

eqc read every bare `Component` its scope did not bind as `this._component`, a field of the State
classes the runtime no longer has. The element engine's `Component = component;` wrote a field nothing
reads, its property stayed null in the browser, and nothing failed; an app's own member of that name met
the same, its declaration and its readers through a receiver using `component`.

Measured on main, the conformance case this change adds fails there (.NET `"x"`, JavaScript `null`).

## What Changes

- A bare `Component` is read by its symbol, as every other name is: a member is that member, written
  `component`; a local, a parameter or a local function of that name is that binding. With no model, the
  capital reads as a member, as every other name does.
- The rule that mapped it to `this._component`, and the scope check it needed, are gone.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-names`: one new requirement.

## Impact

- **eqc**: `IdentifierStrategy`.
- **The public surface and the runtime**: unchanged.
