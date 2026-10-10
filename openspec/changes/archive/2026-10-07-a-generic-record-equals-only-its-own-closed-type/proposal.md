# Proposal

Closes #651, a Bug under #565 (the transpiler's fences hold on every path, continued).

## Why

In .NET a record's equality includes its EqualityContract, the constructed type, so `Box<int>` and
`Box<double>` are never equal. The twin is one `Box` class for every type argument, so both values were
a `Box` holding 1: `new Box<int>(1).Equals((object)new Box<double>(1))` is false in .NET and was true
in the browser, and so was a `List<object>`'s `Contains` across the two.

## What Changes

- Where C# names a generic record's or struct's type arguments (`new Box<int>(1)`, `new(1)` typed
  `Box<int>`), the build marks the value with them (`$eq.closing`), held aside by the runtime so the
  value's members and JSON are its own alone.
- A generic record's or struct's `equals` compares the marks (`$eq.sameClosure`), and `with` carries
  the mark to its copy. A value built inside generic code, or rebuilt from the wire, carries none and
  is not taken for another type, which keeps every comparison that was true true.
- A record that declares no type parameter is written as before.

For a developer: nothing to write. A generic record compares as .NET compares it.

## Impact

- eqc (`ObjectCreationStrategy`, `RecordTypeEmitter`) and the runtime (`eq.ts`). Public surface:
  `Eq.Closing` and `Eq.SameClosure`, two constants of the helper table, are added.
- Not covered: a value built where the type arguments are a type parameter (`new Box<T>(value)`
  inside a generic method) carries no mark, so it still equals one of another closed type; and the
  vocabulary's `ServerTopic<T>`, whose twin the runtime writes by hand, is left to the change that
  brings it.
