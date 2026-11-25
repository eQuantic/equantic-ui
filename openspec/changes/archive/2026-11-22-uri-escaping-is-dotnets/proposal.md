# Proposal

Closes #1805, a Bug under #1587 ("The transpiler's fences hold on every path, part five").

## Why

`Uri.EscapeDataString` was lowered to `encodeURIComponent`, which keeps `! ' ( ) *` where .NET escapes them, so a
link a component built changed once the page hydrated: a real site's WhatsApp link carried `%21` and `%28` from
the server and `!` and `(` from the browser, which `HydrationAssert` reported. Measured on .NET 10, the three
calls differ further from the browser's functions:

- `EscapeUriString` keeps `[` and `]`, which `encodeURI` escapes;
- both escapes write a lone surrogate as U+FFFD (`%EF%BF%BD`), where the browser's functions throw;
- `UnescapeDataString` leaves an escape that is no escape (`%`, `%zz`) and bytes that are no well-formed UTF-8
  (`%C3%28`, `%FF`, a surrogate or an overlong form written in UTF-8) as they are written, decoding what is
  around them, where `decodeURIComponent` throws and takes the page down.

## What Changes

- The runtime escapes and unescapes as .NET does (`$eq.uri.escapeData`, `escapeUri`, `unescapeData`), each
  refusing a null text with the `ArgumentNullException` .NET throws, and the transpiler lowers the three calls
  to them. A developer writes nothing: `Uri.EscapeDataString` in a component draws the same link on both sides.
- `UnescapeUriString`, which .NET does not have, is no longer matched by name.
- The conformance suite's compilation references `System.Uri`'s assembly, which no case could reach before.

eqc's strategy and the runtime are the parts reached. The public surface and the developer surface do not move.

## Capabilities

### Modified Capabilities

- `transpiler-bcl`: `Uri`'s escaping is .NET's.

## Principles

A translation executes identically on both sides, which the conformance suite holds by running each case on
both: the browser's nearest function is a translation only where it answers what .NET answers.
