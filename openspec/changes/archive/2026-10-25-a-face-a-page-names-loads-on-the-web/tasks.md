# Tasks

## 1. The server

- [x] 1.1 `FaceSink`, armed around a render and a discovery expansion as `StyleSink` is, filled by the
      text's lowering with the family it names
- [x] 1.2 `ServedFonts` as a singleton the render and the routes share, learning a family's files the
      first time a page names it, whole or not at all
- [x] 1.3 A rendered page writes the rules of the families it named that the theme's stylesheet does not
      declare into an `eq-faces` style element in its head, once each
- [x] 1.4 `/_equantic/fonts/<assembly>/<resource>` redirects to the file's content-addressed URL, for a
      face an eQuantic build embedded in an assembly the app's deployment carries, and for nothing else
- [x] 1.5 Check: a page that names a family outside the theme declares it once and its URLs answer the
      files; a family the theme declares is not repeated; the identity route redirects, and refuses an
      assembly that is not loaded and a resource no eQuantic build embedded as a face; the prefix it takes
      is the one both SDKs embed under (`FontServingTests`)

- [x] 1.6 The faces outside the theme's stylesheet read once from the app's deployment, so every
      instance answers every URL; a family over a font no eQuantic build embedded as a face is not served
- [x] 1.7 Check: a second instance, which never rendered the page, answers its URL and the identity; a
      family over a font a library embedded another way is declared on no page (`FontServingTests`)
- [x] 1.8 The faces outside the theme's stylesheet are read from every assembly the host lists for the
      deployment too, so a family that names its assembly only as text is served
- [x] 1.9 Check: a face in an assembly the deployment carries, which nothing the app scans references, is
      served (`FontServingTests`); and a probe app over a library it names only as text, whose page
      declares the face and whose identity redirects, where it was refused and logged before

## 2. The browser

- [x] 2.1 The runtime declares a family the first time it sets or measures text in it, unless the
      document has a face of it, and takes the family back whole when a file fails to load
- [x] 2.2 The declarations are `@font-face` rules with the family as a CSS string; one listener the boot
      sets takes back any family a face of which fails, whoever declared it, and rebuilds the pages that
      measured in it
- [x] 2.3 Check: the declarations' tests (declared once, a CSS string, left to the server's head, a family
      the server declared taken back whole, one that failed before the runtime, the pages that measured in
      it rebuilt); and a probe app in a browser, on a page the server rendered and on one the browser built
- [x] 2.4 The runtime asks for every file of a family before it declares it, and rebuilds the pages that
      measured in it once it does; a family named without files, and the theme's face that closes every
      stack, count among what a measure depends on
- [x] 2.5 Check: a missing italic no text uses keeps the family undeclared; a page that measured before
      the declaration rebuilds after it; a page that measured in a family named without files, or in one
      the machine lacks, rebuilds when the face behind it is taken back; and the probe app's upright page
      in a browser, which asks for both files and declares neither

## 3. Documentation

- [x] 3.1 The wiki's DesignSystem page, in all five languages: a family a page names loads on the web too
- [x] 3.2 One ledger entry citing #1056, as a file of its own under `docs/ledger/`
