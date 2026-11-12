- **2026-10-31 · A module imports what it writes, by the name it writes**: three bugs under #565, #849 and #845
  ([#824](https://github.com/eQuantic/equantic-ui/issues/824), [#890](https://github.com/eQuantic/equantic-ui/issues/890),
  [#934](https://github.com/eQuantic/equantic-ui/issues/934)). A type named by a word JavaScript reserves, or by a
  global the emitted code reads, is written under its name and a `$` (`function$`, `Math$`), its module too, as a
  local of that name already was: `class function` went out as `export class function`, which no module parses,
  and a file that called the solution's `Math` and `System.Math` wrote both as `Math`, under an import of the
  class. The rule is the shared `TwinName`, with the reserved words and the globals beside it in `JsNames`, and
  every site that introduces a twin writes the name it registers, a type read through its namespace and a local
  that starts null among them, which named `Item` for `Cart.Item` too. A component's action keeps the id #1290
  gave it, its CLR full name, whatever its twin is named. And a page that reads a string imports no `./Strings`:
  the scan asks the symbol whether a class is a resx Designer, as the parser does, and a name inside a string is
  no reference. Proposed and archived through OpenSpec (`module-imports`, `transpiler-names`).
