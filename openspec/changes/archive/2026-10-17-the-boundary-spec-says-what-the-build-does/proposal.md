# Proposal

Closes #945, a Bug under #849 (the transpiler's fences hold on every path, continued).

## Why

The `client-boundary` spec stops before #944's last commit and says one thing the build does not do.
Measured on main at 1695e2c98, through the build loop eqc runs:

- The spec refuses "a type argument in a component's or a class's base list". The build never refused
  it, and is right not to: a component's twin implements no interface and a class's twin extends its
  base without the argument, so the module writes the type as nothing. `Home : StatelessComponent,
  IMark<Ledger>`, `Home : Shell<Ledger>` over a generic base component and `LedgerHolder :
  Holder<Ledger>` build, in the app and from a library, and no module names `Ledger`.
- The spec refuses a Server Action's parameter that names a kept-out type, and #944 made a dictionary's
  free, since its stub annotates it `any`.
- #944's last commit refuses a `[ServerOnly]` `GetEnumerator`, `MoveNext`, `Current` or `Dispose` that
  a spread or a LINQ operator reaches, and a call through a covariant or contravariant receiver, and
  stops counting a string literal as a type reference. The spec says none of it.

## What Changes

- The spec says what the build does: the enumerator's protocol in `foreach`, in a spread and in a LINQ
  operator, the variance of a receiver, a base list's type argument and a dictionary's written as
  nothing, and a string literal that holds a type's name.
- A test pins the base list as free, for a component's header and a class's, in the app and from a
  library.
- Nothing changes for a developer: the build answers as it did since #944. The public surface and the
  developer surface do not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client-boundary`: three requirements say what the build does since #944, each with the scenarios
  that show it.

## Impact

eqc's behaviour is unchanged. The spec, a test in `eQuantic.UI.Compiler.Tests`, the ledger and the
wiki's pages that state the boundary, in their five languages.
