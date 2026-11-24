# Proposal

Closes #1854 and #1860, two Bugs under #1587 (The transpiler's fences hold on every path, part five), met by #1563's
`GestureRecognizerFactoryWithHandlers<T> where T : GestureRecognizer` once #1858 and #1859 had removed the other
errors of its library twins.

## Why

Since #1845 a generic class declares its type parameters in TypeScript, but by their names alone. Two shapes of #1563's
library still fail the runtime's `tsc`:

- A type parameter's constraint is not written (#1854), so a member read on a `T` constrained to a class has nothing to
  resolve against (TS2339), and an override answering `T` where its base answers the class is refused (TS2416).
- A cast to a type parameter is written as the value alone (#1860), so the call it feeds reads the operand's type where
  it takes a `T` (TS2345).

## What Changes

- A type parameter constrained to a class that is not generic and has a twin is declared `T extends` that class in
  TypeScript, the class imported as any name the module writes. Whether a class has a twin is asked as a downcast asks
  it (`CastExpressionStrategy.NamesATwin`, now shared), so the two never disagree about a class the server keeps,
  which the client fence refuses before either is asked (EQ2010). Any other constraint (an
  interface, which has no twin, a generic class, `struct`, `class`, `new()`, `notnull`) leaves the parameter
  unconstrained, as before. A member still names the class's own closure by the names alone.
- A cast to a type parameter is asserted in TypeScript, `(value as T)`, checking nothing, as a downcast checks nothing.
  Plain JavaScript writes the value alone, as it did.

Nothing an app writes moves, and no module's JavaScript changes.

## Impact

- `src/eQuantic.UI.Compiler`: `TypeSyntaxExtensions.TwinTypeParameters`, the class and record writers' constraint namers,
  `CastExpressionStrategy`
- The emission shapes the runtime's `tsc` reads: `EmittedMeasured`, `EmittedTagged`, `EmittedHandler`
- `openspec/specs/transpiler-classes`: the requirement that a class's TypeScript twin passes the runtime's type check
- An entry under `docs/ledger/`
