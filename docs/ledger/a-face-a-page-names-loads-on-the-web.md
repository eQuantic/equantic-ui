- **2026-10-25 · A face a page names loads on the web as it does on Photon**: the third slice of #1038
  ([#1056](https://github.com/eQuantic/equantic-ui/issues/1056)). A family a `Text` names at a call site
  loads wherever it comes from, built by hand or read from a library's catalog. A page the server renders
  declares, in an `eq-faces` style element in its head, the faces of every such family the theme's
  stylesheet does not declare, collected while it renders (`FaceSink`, beside `StyleSink`). A page the
  browser builds declares a family the first time the runtime sets or measures text in it, as the same
  `@font-face` rules, unless the document has it, asking for each file by its identity at
  `/_equantic/fonts/<assembly>/<resource>`, which the server sends on to the hashed URL. The web serves a
  face the theme names, or one an eQuantic build embedded in the app's assemblies, read from the
  deployment so every instance answers every URL: a family over a font a library embedded another way is
  served nowhere, and logged. A family is declared once, falls back whole when a file fails to load,
  whoever declared it, and the pages that measured text in it measure again. `ServedFonts` became a
  service the render and the routes share. Proposed and archived through OpenSpec (`font-faces`).
