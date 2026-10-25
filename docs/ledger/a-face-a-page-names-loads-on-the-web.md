- **2026-10-25 · A face a page names loads on the web as it does on Photon**: the third slice of #1038
  ([#1056](https://github.com/eQuantic/equantic-ui/issues/1056)). A family a `Text` names at a call site
  loads wherever it comes from, built by hand or read from a library's catalog. A page the server renders
  declares, in an `eq-faces` style element in its head, the faces of every such family the theme's
  stylesheet does not declare, collected while it renders (`FaceSink`, beside `StyleSink`), and the
  server serves their files by the hash of their bytes. A page the browser builds declares a family the
  first time the runtime sets or measures text in it, unless the document has it, asking for each file by
  its identity at `/_equantic/fonts/<assembly>/<resource>`, which the server sends on to the hashed URL
  for a face an eQuantic build embedded and nothing else. A family is declared once, and falls back whole
  when a file fails to load. `ServedFonts` became a service the render and the routes share. Proposed and
  archived through OpenSpec (`font-faces`).
