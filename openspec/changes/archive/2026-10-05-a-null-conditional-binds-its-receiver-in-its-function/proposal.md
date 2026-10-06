# Proposal

Closes #539, a Bug under #164 (The transpiler's fences hold on every path).

## Why

A null-conditional call whose translation is a helper bound its receiver with an arrow invoked on
the spot, `(($r) => $r == null ? null : $eq.text.trim($r))(get())`, and an `await` in its arguments
landed inside that arrow, a module JavaScript refuses to parse. #536 fixed the receivers it could
read again (a local, a parameter, `this`) and refused every other one with EQ1004, asking the
developer to bind the receiver to a local first. That refusal is the last site of #539's class:
`Get()?.StartsWith(await Needle(), StringComparison.Ordinal)` is ordinary C#, and the developer
should not have to rewrite it. Measured through the conformance harness, the 6 cases this change
adds with an `await` behind a call, an element, a property and a guard in the tail of another fail
on main.

## What Changes

- **Any receiver binds to a temporary of the function the C# is written in.** A call, an element or
  a property in front of `?.` is assigned to a temporary the statement around it declares,
  `(($n0 = get()) == null ? null : $eq.text.trim($n0))`, with `let $n0;` in front of the statement.
  The tail runs in the method itself, so an argument that awaits is awaited there, only when the
  receiver is not null, and nothing the call answers is awaited. EQ1004's "bind the receiver to a
  local first" is gone: the developer writes the C# as they would for .NET.
- **Each function declares its own.** A concise lambda that binds one gets a block that declares it,
  so a lambda run several times at once keeps each call's receiver. A statement declares it in front
  of itself (a body written without braces gets its braces), and a loop a label names leaves it to
  the label, in front of it, since JavaScript continues a label only on the loop it stands on.
- **Where nothing can declare one**, an initializer or a record's base call, the arrow stays: C# lets
  no initializer await, and the arrow runs once each time the initializer does.

What does not move: no public C# signature and nothing on the developer surface. The only code an
app sees change is the emitted JavaScript of such a call. The component library's twins move in one
place, `DataTable`'s selection test.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-expressions`: the null-conditional requirement stops refusing a receiver that is not a
  local and binds it in the function the C# is written in.

## Impact

- eqc only: `ConditionalAccessStrategy`, the converter's statement dispatcher and its one lowering
  of a concise body, the lambda strategy, and the conversion context, which gains the temporaries.
- The runtime is untouched; one twin (`DataTable.ts`) is regenerated.
- The wiki's Compiler page, in English and Portuguese, stops saying the build fails.
