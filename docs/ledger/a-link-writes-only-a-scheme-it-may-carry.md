- **2026-11-09 · A link writes an href only for a scheme it may carry**: no URL scheme was checked
  anywhere, so a `Link`'s, a `TextRun`'s or a Markdown link's destination reached `href` with whatever
  scheme it carried, in SSR and on the client. The web realizer and its
  twin now write a destination only with no scheme or with `http`, `https`, `mailto` or `tel`, read as a
  browser's URL parser reads it, and write the anchor with no `href` for any other
  ([#1286](https://github.com/eQuantic/equantic-ui/issues/1286)). `RenderContext.ResolveDestination`
  answers `string?`. Proposed and archived through OpenSpec (`openspec/specs/links`).
