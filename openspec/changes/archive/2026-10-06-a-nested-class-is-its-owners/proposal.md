# Proposal

Closes #584, a sub-issue of #565 (the transpiler's fences hold on every path, continued). It builds on
the class construction of #583, whose branch this one follows.

## Why

A type declared inside another one is its owner's scope in C#, and eqc had no single answer for it,
measured through the module graph an app's build writes, both sides executed:

- A nested class that is not a component's static helper got no module, so constructing it named a
  class the browser does not have: `Roster.First()` building a private `Row` answered "Ada" in .NET
  and threw in the browser.
- Where a top-level class has the nested one's name, the nested one resolved to it:
  `new Cart().Size()` over a private `Cart.Item` with `Qty = 9`, beside a top-level `Item` with
  `Qty = 3`, was 9 in .NET and 3 in the browser.
- A nested record or struct got a module named by its simple name, which a top-level type of that
  name writes too, one file over the other.
- A nested static class outside a component got no module, and one inside a component was written
  into the component's module under its simple name.

## What Changes

- Every class, record and struct declared inside another type has a twin of its own, named by the
  chain of the types that contain it and its own name, joined by `$` (`Cart$Item`, `A$B$C`): a name no
  C# type can take, so it never meets a top-level type's. Its module is named so, and every reference
  to it, inside its owner or out, a construction, a type test, a static member, a default and an
  annotation alike, names it so, importing it from there.
- A nested type of an owner that never crosses (`[ServerOnly]`, an exception, an attribute) has none,
  as its owner has none.
- A record's text still prints its C# name (`Line { Qty = 2 }`), as .NET's does.
- The runtime's one nested type, `CodeBlock.CodeMetrics`, is written as `CodeBlock$CodeMetrics`. The
  public surface of `eQuantic.UI.Compiler` and the developer surface do not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-classes`: a nested class, record or struct is a module of its own, named by its owner.

## Impact

- eqc: one name for a type's twin (its containing types and its own name), which the parser, the
  dependency resolver, every emitter and every strategy that names a type in the code it writes read;
  the parser discovers nested types as modules; a component no longer writes its nested static
  classes into its own module.
- The runtime: `CodeMetrics` regenerates as `CodeBlock$CodeMetrics`.
- Tests: conformance cases through the module graph for each shape, both sides executed.
- Docs: the wiki's SupportedFeatures page (English and Portuguese) and one docs/LEDGER.md line.
