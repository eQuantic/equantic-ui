## Context

`PatternConverter` reads a property subpattern's member as a member access of it reads it (`Read`, #1163),
and wrote that read at every place the operation names it: each alternative of an `or`, each conjunct of an
`and`, each binding, each arm of a switch expression and each section of a switch statement, and each member
before the last of an extended path, once for its null test and once as the next member's receiver. A
property's getter runs at each of them. `MatchParts` already holds what a `Deconstruct` the app wrote hands
back once for the operation (#696), declared where the operation declares its temporaries, so each evaluation
starts it over.

## Goals / Non-Goals

**Goals:** each member a property pattern reads whose read runs the app's code is read once per value per
pattern-matching operation, and again by each evaluation of it; a member nobody can observe being read is
written in place, so the transpiled pins change only where a getter of the app's ran more than once.

**Non-Goals:** the parts of a positional pattern over a record read through its own `Deconstruct`, which the
compiler writes over the record's positional properties: those are auto-properties nobody can see read, unless
the record declares one with an accessor of its own. The input of the pattern, read once since #1157.

## Decisions

**How Flutter (Dart) answers it.** Dart 3's patterns give each getter an object pattern calls an invocation
key, and invoke each key once per matched value in a switch or an if-case (the conformance suite's
`invocation_keys` tests check it), the promise C#'s decision graph makes per operation. There is no Flutter
mechanism to borrow: it is each language's own rule, and eqc meets C#'s.

**A member's read is held where a `Deconstruct`'s parts are.** `MatchParts` keys a temporary by the value's
text and the symbol it reads, a `Deconstruct` or a member, so the same member of the same value is one
temporary across every pattern of the operation, a member of a held member included (`Corner`, then its `X`).
The operations already declare what it holds: no statement around an `is` is asked for, and an initializer is
held as any method body is.

**The first test that reaches it reads it.** Every mention is written alike, `($p ?? ($p = [read]))[0]`, and
whichever runs first assigns it. Reading the member eagerly, at the top of the operation, would read a member
.NET never reads: an arm whose type test or null test fails first reads nothing after it.

**What is held is a box.** A member may answer null, and `$p ?? ($p = read)`, the form a `Deconstruct`'s parts
take (every method with outs answers an object), would read a null member again at the next mention. The box,
`[value]`, never is.

**What runs the app's code is asked of the symbol.** A property the app declares with an accessor of its own, an
abstract, virtual or overriding one, and an interface's run code an override or an implementation may supply,
where the property or the value's type is the app's. A field, an auto-property the twin keeps under its own name
(`PropertyStore.IsKeptUnderItsName`, the rule `SlotOf` already followed) and a property of the platform's read off
a value of the platform's run none of it, and are written in place.

## Risks / Trade-offs

An `is` that holds a member is written in the arrow of its own that already holds a `Deconstruct`'s parts, one
closure per evaluation, and only where a getter of the app's is read.

A property of the platform's that an override of the app's answers for, read off a value whose type is the
platform's (`Exception e; e is { Message: "a" or "b" }`, `e` an exception of the app's that overrides `Message`
with a getter that has an effect), is still read at each mention: the build cannot see the value's type there,
and holding every virtual member of the platform's would hold every `{ Count: > 0 }` over an `IReadOnlyList<T>`
in the components, whose reads nobody can see. Read off a value of the app's type, it is held.

A held value is typed `any` in TypeScript, as every temporary the scanner declares is.
