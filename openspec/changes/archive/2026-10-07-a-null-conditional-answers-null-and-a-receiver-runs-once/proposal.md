# Proposal

Closes #633, #619 and #655, three Bugs under #565 (The transpiler's fences hold on every path, continued).

## Why

A null-conditional read whose translation is JavaScript's optional chain, `a?.B` as `a?.b`, is
`undefined` in the browser when `a` is null, where C# answers `null`. The two part ways wherever the
value is used: a parameter the emitter types `T | null` refuses it in the runtime's own build, JSON
drops a key that holds it, and a dictionary that looks for null does not find it. Measured through the
conformance harness, `ContainsValue(null)` over a value read that way answers false in the browser and
true in .NET (#633).

A method group bound to its receiver wrote the receiver twice, once to read the method and once to bind
it, so a receiver that is a call ran twice: `c.Make().Value` emitted `c.make().value.bind(c.make())`,
and a counter it bumps answers 4 where .NET answers 2 (#619).

The review of this change found the same two families one step further. A chain whose outer link is a
guard, because the call between the links is not rooted at the receiver, handed the inner chain's
undefined out as it was: `(h == null ? null : Ext.pick(h.name)?.length)` (#633 again). And a method
group was bound to its receiver as an instance member in every case: `base.Sound` emitted
`super.sound.bind(super)`, which JavaScript refuses at parse, and `s.Twice` over an extension method
emitted `s.twice.bind(s)`, a member a string never has (#655).

## What Changes

- **An optional chain answers null where its value is used.** `a?.b` becomes `(a?.b ?? null)` where the
  value goes on to something: an assignment, an argument, a return, an operand. It stays a bare chain
  where nothing can tell the two apart: a call that returns nothing (C# never lets its value be used),
  a statement that discards it, the left of a `??`, and the tail of another chain, which answers where
  the chain ends.
- **A method group reads its receiver once.** The bind is a template over the receiver, so a receiver
  that is not a plain name is evaluated once, as C# evaluates it when the delegate is made, and `this`
  stays inline: `this.value.bind(this)`.
- **A guard settles a tail that is a chain of its own.** Behind `h == null ? null :` the inner chain
  answers null where the value is used, as it does when the outer link is a chain.
- **A method group on base binds this.** `base.Sound` makes `super.sound.bind(this)`, the base's method
  called on this object.
- **An extension's method group goes to its home.** The decision a call takes, its home's static with
  the receiver first, is split out of `InvocationStrategy.Extension` as `ExtensionHome`, and the group
  binds there with the receiver read once: `Ext.twice.bind(Ext, s)`. A group over an extension that
  nothing in the bundle declares, a BCL one such as `list.Any`, fails the build with EQ2004 instead of
  throwing in the browser.

What does not move: the developer's C#, the developer surface and the public API. Two twins of the
runtime's library change where a chain's value is used or a method group binds a field's object:
`CodeDiffLayout` and `Spreadsheet`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-expressions`: a null-conditional read answers null where its value is used, behind a
  guard too, a method group reads its receiver once, and a method group on base or of an extension binds
  what C# binds.
