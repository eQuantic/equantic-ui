## Why

Closes #1165, a task under #849 (the transpiler's fences hold on every path, part three).

#527 made a char literal cross from its value, through `JsStringLiteral.Quote`, where it had crossed as
its C# token text (#520). Its conformance cases name `'\a'`, `'\e'`, `'\x1'`, `'\x041'` and
`'\U00000041'`, and leave out two spellings JavaScript reads otherwise when it is handed the token, a
leading zero and four hexadecimal digits, and `'\0'`, a space and a letter, which it reads alike.
Measured in the embedded Bun and in Node, the token text of `'\x007'` is two characters (NUL and "7")
and of `'\x0041'` three, where C# reads one character each, and `'\x7'` is a SyntaxError, as `'\x1'` was.

## What Changes

- `LiteralConformanceTests` runs every spelling of a char on both sides, each in a case of its own and
  read back as its length and its code: `'\a'`, `'\e'`, `'\x7'`, `'\x007'`, `'\x0041'`, `'\0'`, a space
  and a letter.
- The requirement "A char literal is its value" names them in a scenario of its own.
- Nothing a developer using the SDK writes or meets changes: the translation already gives these
  answers, and the five spellings JavaScript reads otherwise fail on the token-text arm it replaced,
  each on its own.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-expressions`: the requirement "A char literal is its value" gains the scenario that
  names every spelling of a code unit. Its text does not move.

## Impact

- **Tests**: the Conformance suite, `LiteralConformanceTests`.
- **eqc, the runtime, the realizers, the shells, the SDKs**: none.
- **Public surface**: none. The developer surface does not move.
