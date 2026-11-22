- **2026-11-22 · Uri's escaping is .NET's in the browser**: `Uri.EscapeDataString` was lowered to
  `encodeURIComponent`, which keeps `! ' ( ) *`, so a WhatsApp link a real site built in a component changed once
  its page hydrated. Measured on .NET 10, `EscapeUriString` keeps the `[` and `]` that `encodeURI` escapes, both
  write a lone surrogate as U+FFFD where the browser threw, and `UnescapeDataString` leaves a malformed escape as
  written where `decodeURIComponent` threw. The runtime escapes and unescapes as .NET does (`$eq.uri`), 33 cases
  run on both sides, and the fixture site's share page hydrates with no difference
  ([#1805](https://github.com/eQuantic/equantic-ui/issues/1805)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-bcl`).
