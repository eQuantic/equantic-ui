# Proposal

Closes #1882, a Bug under #1587 (The transpiler's fences hold on every path, part five). A real site lost its order
pages to it.

## Why

Since #1805, eqc writes `Uri.EscapeDataString(x)` as `$eq.uri.escapeData(x)`, and the strategy that writes it never
registers the `$eq` import. A record's and a component's module import `$eq` wherever their text names it, but a class
module took it only from the strategies that registered it. So a static class whose one runtime call was the escape
(`OrderRoutes.Pay`) shipped a module with no import. The server's HTML was right, the module loaded, and the page threw
"ReferenceError: $eq is not defined" the first time the method ran, once it had hydrated.

## What Changes

- A class module puts `$eq` among its candidates, as a record's and a component's do, and keeps it where the emitted
  text names it. The import follows from what the module writes, whoever wrote it, so a strategy that forgets to
  register it no longer ships a module that fails.

No module that names `$eq` loses its import, and no module that does not name it gains one. Nothing an app writes
moves.

## Impact

- `src/eQuantic.UI.Compiler`: the class module's imports in `TypeScriptEmitter`
- The emission shapes the runtime's `tsc` reads: `EmittedRoutes`
- The conformance suite: a static class whose only runtime call is an escape, built as an app builds it
- `openspec/specs/module-imports`: a new requirement, that every module imports the runtime's helpers where its text
  names them
- An entry under `docs/ledger/`
