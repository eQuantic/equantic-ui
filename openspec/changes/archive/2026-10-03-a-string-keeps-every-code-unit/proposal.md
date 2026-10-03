# Proposal

Closes #520, a Bug under #164 (The transpiler's fences hold on every path), and closes #491, whose
string-literal half #412 had already fixed.

## Why

eqc wrote C# string and char values into the module in spellings that did not keep them. A
surrogate that is not half of a pair has no UTF-8 encoding, so a page holding `"x\uD83D"` stopped
the whole build with `EQ0001: Compilation crash: Unable to translate Unicode character`, naming no
file; a char literal crossed in its C# spelling, which JavaScript reads its own way (`'\a'` was
`'a'`, `'\x1'` a syntax error); and four writers quoted a value by hand, so
`$"{date:dd 'de' MMMM}"` closed its own quotes and Bun refused the module. Measured on main at
d09f7bec with the dashboard sample and with the conformance harness, which could not even run a
case holding a lone surrogate.

## What Changes

- **One writer for every string the transpiler spells.** A string literal, a template literal's
  text, an inlined `const`, an interpolation's format, the default a named argument skips, a
  resource lookup's id and key, and `nameof` all go through it. It escapes each code unit that does
  not show as itself: a surrogate that is not half of a pair, a control, a format character, U+2028
  and U+2029, a private-use character, a space other than U+0020, and a combining mark with nothing
  written before it to sit on. Everything else, a well-formed pair included, is written raw.
- **A char literal and `nameof` are their value, not their spelling.** `'\a'`, `'\e'`, `'\x041'`,
  `'\x1'` and `'\U00000041'` are the characters C# reads; `nameof(@class)` is `"class"`.
- **A raw interpolated string keeps its braces.** `$$$"""a{{b}}"""` is `a{{b}}`; only a regular or a
  verbatim interpolated string reads a doubled brace as one.
- **An interpolated string's text escapes U+2028 and U+2029** as the literal's already did, so a frame
  thrown after one maps to its own line (#491).
- **A literal with no JavaScript spelling is refused.** A UTF-8 literal (`"ab"u8`) and `__arglist`
  were spliced into the module as C#; the build now fails with EQ1004 at the literal.
- **The text copies of a default go.** `StateField.DefaultValue` and `PropertyDefinition.DefaultValue`
  held the initializer's source text for a writer no path reached, which quoted a string by hand.

For a developer using the SDK: any string or char a component writes reaches the browser with the
value C# holds, whatever its escapes or code units; a date format with quoted text builds; and a
literal the browser cannot hold is a build error at its line instead of a crash or a module Bun
refuses.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-expressions`: "A string crosses whole" widens from line breaks to every code unit and
  every writer, and gains the char literal, `nameof`, the raw interpolated string's braces and the
  refusal of a literal with no JavaScript spelling.
- `transpiler-source-maps`: a string holding U+2028 or U+2029 moves no later line of the map.

## Impact

- The compiler: `JsStringLiteral`, `LiteralExpressionStrategy`, `InterpolatedStringStrategy`,
  `ObjectCreationStrategy` (a skipped parameter's default), `ResourceAccessorStrategy`,
  `NameofStrategy`, `TypeScriptEmitter` and `ComponentParser`.
- The runtime's transpiled components change spelling only: a tab inside a string is `\t`, a char
  `'\0'` is `'\u0000'`; every value is the same.
- Public surface: `StateField.DefaultValue` and `PropertyDefinition.DefaultValue` go, with nothing in
  their place (`DefaultValueNode` is the initializer). `PublicAPI.Unshipped.txt` carries the four
  `*REMOVED*` lines. The developer surface does not move, and no app needs a migration line.
