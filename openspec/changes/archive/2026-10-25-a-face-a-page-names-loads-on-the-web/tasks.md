# Tasks

## 1. The server

- [x] 1.1 `FaceSink`, armed around a render and a discovery expansion as `StyleSink` is, filled by the
      text's lowering with the family it names
- [x] 1.2 `ServedFonts` as a singleton the render and the routes share, learning a family's files the
      first time a page names it, whole or not at all
- [x] 1.3 A rendered page writes the rules of the families it named that the theme's stylesheet does not
      declare into an `eq-faces` style element in its head, once each
- [x] 1.4 `/_equantic/fonts/<assembly>/<resource>` redirects to the file's content-addressed URL, for a
      face an eQuantic build embedded in an assembly the app has loaded, and for nothing else
- [x] 1.5 Check: a page that names a family outside the theme declares it once and its URLs answer the
      files; a family the theme declares is not repeated; the identity route redirects, and refuses an
      assembly that is not loaded and a resource no eQuantic build embedded as a face; the prefix it takes
      is the one both SDKs embed under (`FontServingTests`)

## 2. The browser

- [x] 2.1 The runtime declares a family the first time it sets or measures text in it, unless the
      document has a face of it, and takes the family back whole when a file fails to load
- [x] 2.2 Check: the declarations' tests (declared once, left to the server's head, taken back whole); and
      a probe app in a browser, on a page the server rendered and on one the browser built

## 3. Documentation

- [x] 3.1 The wiki's DesignSystem page, in all five languages: a family a page names loads on the web too
- [x] 3.2 One ledger entry citing #1056, as a file of its own under `docs/ledger/`
