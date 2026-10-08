# Proposal

Closes #569, a sub-issue of #565 (the transpiler's fences hold on every path, continued).

## Why

A .NET method that cannot take a null refuses it with an `ArgumentNullException` naming the parameter.
Several runtime twins read the argument first, so the browser threw a `TypeError`, which the runtime
reads as a `NullReferenceException`, and since #561 a `catch (ArgumentNullException)` around such a call
lets it through. #561 fixed the seven its review named, one by one, and the rest of the surface was not
looked at. One fix per report never ends: the issue asks for one measurement instead.

## What Changes

- **A conformance theory measures every null argument of the translated surface.** The surface is
  derived: its types are the ones the BCL audit's committed record names, its members what reflection
  finds on them, constructors, indexers and generic methods included, and eqc's own diagnostics say which
  it translates. Each member is called with a null for each reference parameter on both sides, and the
  exception's type, its `ParamName`, its message where the SDK composes it, and the value a call that
  returned answered are compared. Each probe runs a control, the same call without the null, so a member
  that differs whatever the argument is recorded as such rather than charged to the null.
- **The gaps are a committed baseline that may only shrink**, each with its aspect and a reason, and a
  guard fails where a member the audit grades `native` or `eq` takes a reference parameter and no null
  reaches it on both sides.
- **The twins that read a null through null refuse it by name, as .NET does**: LINQ's `Max`, `Min` and
  `ToDictionary`; a sequence a LINQ operator reads through `seq`, named by the parameter it binds to
  (`second`, `inner`, a `Zip`'s `first`), where every one was `source`; `CompareTo(object)`, which
  answers 1 for a null; `new Guid(text)`, which names its text `g`; `char.GetUnicodeCategory(text, i)`;
  and the cancellation pair's `Register` and `CreateLinkedTokenSource`.

For a developer using the SDK: a null handed to one of those members is caught by
`catch (ArgumentNullException e)`, and `e.ParamName` names .NET's parameter. A member lowered to
JavaScript's own method, LINQ over a list, a string's search and an array's among them, still reads a
null through null, a `NullReferenceException`: refusing it by name means a guard at every call, which is
a design decision, and the baseline lists each one with that reason. Nothing an app writes changes.

The public surface gains `Eq.GuidOf`, the constant for `new Guid(text)`'s reader. The developer
surface does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-exceptions`: a null argument is answered as .NET answers it, measured over the translated
  surface.

## Impact

- eqc: `LinqSource` (a sequence named by its parameter), `CompareToStrategy` (a null object answers 1),
  `GuidTypeStrategy` and `Eq` (the constructor's reader).
- The runtime: `utils/linq.ts`, `utils/datetime.ts`, `utils/guid.ts`, `utils/cancellation.ts`,
  `utils/unicode-category.ts`, and `eq.ts` for `guid.of`. The transpiled twins that concatenate
  sequences are regenerated: their `seq` calls name `first` and `second`.
- Tests: `NullArgumentConformanceTests` and its baseline `null-argument-gaps.baseline.txt`, both sides,
  with the harness's `NullArgumentSurface` and `NullArgumentRun`.
