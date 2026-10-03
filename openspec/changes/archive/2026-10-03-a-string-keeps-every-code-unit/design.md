# Design

## Context

See proposal.md for why. The transpiler had ten writers of a JavaScript string: `JsStringLiteral`
(the literal strategy, an inlined `const`, a component's `$typeId`), the interpolated string's own
escaping of its text, five writers that quoted a value by hand (an interpolation's format, a skipped
parameter's default, the resource lookup, `nameof`, and an unreachable default writer in the
emitter), and the char literal, which copied its C# token. eqc writes each module with
`File.WriteAllText`, whose UTF-8 encoder throws on a surrogate that is not half of a pair; the
conformance harness writes its program the same way. The bundler re-spells what it ships: measured
with the embedded Bun 1.3.14, a lone surrogate escape and U+2028 stay escapes, a control becomes
`\x07`, a pair becomes two escapes, and text such as `ção` stays raw.

## How Flutter answers it

Every compiler reads a literal into its VALUE and prints the value; nothing re-uses the source
spelling, because the source language's escapes are not the target's. Dart's web compilers have the
same split, so the question this change answers (a value written in a spelling that loses it) does
not arise there. docs/FLUTTER-PARITY.md has no row for literals.

## Goals / Non-Goals

**Goals:** one writer for every string spelled from a C# value; the value C# holds, code unit for
code unit; a module that can always be written as UTF-8; a literal with no spelling refused at its
line.

**Non-Goals:** the spelling of what Bun ships (the bundler decides it); a twin for a UTF-8 literal
(a span of bytes); the numeric representation of a constant outside a literal, the iteration of a
string by code points, the server shell's configuration script and the source map's JSON, each filed
on its own (see tasks).

## Decisions

- **The value, never the spelling.** A char literal is written from `ValueText`, as a string always
  was, and `nameof` from the constant the model binds (the identifier's `ValueText` where no model
  answers). The alternative, translating C#'s escapes into JavaScript's, keeps a table of a
  language's escapes beside the compiler that already decoded them.
- **Escape what does not show as itself.** A code unit is written raw when it shows as itself, and
  as `\uXXXX` otherwise: a surrogate that is not half of a pair (no encoding), a control and a format
  character (invisible, a bidirectional override among them), U+2028 and U+2029 (line breaks to Bun
  when it maps positions, #491), a private-use character (no glyph outside its font), a space other
  than U+0020 (reads as one), and a combining mark with nothing written before it to sit on (it would
  sit on the quote, as `'\uFE0F'` in the code editor's cell measure would have). Escaping every
  non-ASCII code unit, esbuild's default, was rejected: the runtime's transpiled components are read
  in review, an app's text is readable in its module, and the bundler re-spells the shipped file
  anyway. Escaping only the lone surrogate was rejected: a raw control is invisible, and the rule
  would still need a second writer for the template's text.
- **Three short escapes.** The line feed, the carriage return and the tab keep `\n`, `\r` and `\t`;
  every other control is `\uXXXX`, because `\0` before a digit is a legacy octal escape a module
  refuses, and `\b`, `\v` and `\f` are rare enough to read better as numbers.
- **The template's text is the same writer** with its own two delimiters, the backtick and `${`.
  The doubled-brace collapse stays with the interpolated string, which is the only place that knows
  the string's kind: a regular or verbatim one reads `{{` as a brace, a raw one never does.
- **A literal with no spelling is refused with EQ1004** through `ConversionContext.Unhandled`, the
  same door every strategy uses for a form it claimed and cannot emit.
- **The unreachable default writer goes, and the text it read.** `TypeScriptEmitter.ConvertToTsValue`
  ran only when a property or field had a default's text and no default's node, which the parser
  never produced, and it wrapped C# source text in quotes. With it gone, `StateField.DefaultValue`
  and `PropertyDefinition.DefaultValue` are written and never read, so they go too, as
  `ParameterDefinition`'s already had.

## Risks / Trade-offs

- [The runtime's transpiled components change spelling] → a tab inside a string is `\t` and a char
  `'\0'` is `'\u0000'`; the pins are regenerated, and the runtime's own tests run on them.
- [The categories come from .NET's Unicode tables] → a code point a newer .NET assigns can change
  its spelling, never its value; the conformance cases compare values.
- [The harness never reads diagnostics] → the refusal is proved in the compiler's tests, where a
  case asks for EQ1004 and the strategy's own message.
