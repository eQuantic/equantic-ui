- **2026-11-23 · A named argument beside an initializer keeps both**: a vocabulary record built with a named argument
  and an object initializer, `new TextRun(code, Mono: true) { StyleOverride = … }`, passes the initializer in the
  twin's config slot, where the defaults before it were counted from the arguments the syntax wrote, one went in
  twice, and the initializer landed in a slot nothing reads, so the run drew in the sentence's size once the page
  hydrated ([#1756](https://github.com/eQuantic/equantic-ui/issues/1756)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
