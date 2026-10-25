## Context

The server reads its faces once, at startup: every file of the theme's families and of the families in
the catalogs of the assemblies the app scans (`ServedFonts`), declared in the theme's stylesheet and
served at `/_equantic/fonts/<hash>.<ext>`. The route serves nothing else, because a name is the only way
in. A server render arms ambient sinks the lowering fills (`StyleSink` for the atomic classes,
`GradientSink` for the paint servers) and writes what they collected into the page. Photon's shells
register a family the first time text asks for it (`ShippedFaces`).

On the web a node names a face in one place: a `Text`'s style. A role's face rides the theme's
`.eq-type-*` classes, which the theme's stylesheet declares.

## Goals / Non-Goals

**Goals:** a family a `Text` names loads on the web wherever it comes from, on a page the server renders
and on one the browser builds; each family declared once; a family whole or not at all, as on Photon.

**Non-Goals:** a face from a provider (#1057); scanning every referenced library's catalog at startup,
which would declare on every page faces that no page names.

## Decisions

- **How Flutter does it.** Flutter's web build serves a font by its asset path, from the manifest the
  build writes, and a font loaded at run time goes through `FontLoader`. Here the server already knows a
  file's bytes and serves them by their hash, which a browser keeps for as long as they last, and the
  runtime knows a family's files by their assembly and resource. The runtime asks by that identity, the
  asset path's counterpart, and the server answers with the content-addressed URL, so every page and
  every path shares one cached copy of a file.
- **A sink, like the atomic classes'.** `FaceSink` is armed around a render, as `StyleSink` is, and the
  text's lowering adds the family it names. The server writes the rules of what the page collected into
  an `eq-faces` style element in the head, after the families the theme's stylesheet declares are left
  out. A discovery expansion keeps its own sink, so nothing it collects reaches the page.
- **One set of faces, a service, from the deployment.** `ServedFonts` becomes a singleton the render and
  the routes share, in place of a table beside the routes. What it serves is decided by the app's
  deployment and never by the requests an instance has answered: an app behind a load balancer renders a
  page on one instance and is asked for its font by another. Beside the theme's stylesheet it reads, once,
  every face an eQuantic build embedded in the app's own assemblies, the ones it scans, the entry assembly
  and every assembly they reference outside the platform, so any instance answers any of their URLs.
  A family a page names is served when every file of it is one of those, or a file the theme's stylesheet
  serves. One over a font a library embedded another way, which the theme does not name, is served by no
  instance, and the server says so: only the instance that read it could have answered its URL.
- **The identity route is narrow.** `/_equantic/fonts/<assembly>/<resource>` answers only a face an
  eQuantic build embedded, under `eQuantic.UI.Fonts/`, a `.ttf` or an `.otf`, in one of the app's
  assemblies, which the deployment names and no request does, and a file the theme's stylesheet serves,
  which a page may name in a family of its own and every instance read when it started. An assembly may embed a font for something
  else, a PDF writer's, whose licence does not cover the web, and the route must not publish it because a
  browser asked. Its redirect is not cached, so a new build's file is found under its new hash.
- **Rules, not `FontFace`.** The runtime declares a family as the server does, `@font-face` rules in the
  `eq-faces` element with the family's name as a CSS string. Measured in Chromium, `new FontFace` takes its
  family argument as the name itself, quotes included, where the specification parses it as a CSS family
  name, so a quoted name was another family there and `Roboto+Mono` was invalid under the specification. A
  CSS string reads alike in every engine, and a rule is withdrawn by deleting it.
- **Whole or nothing in the browser, whoever declared the face.** The runtime adds a family's rules to
  the document at once, so a page that measured text in them rebuilds when they load (#1055's tracker).
  One listener for the document's `loadingerror`, set by the boot, takes back every rule of a family a
  face of which failed, the theme's, the server's or the runtime's, and a face that failed before the
  runtime was there is taken back when it starts. The text falls back as a whole, as it does on Photon,
  and the pages that measured text in the family rebuild, holding each page's one invalidator weakly, so
  a page that left the tree is not kept alive by a font that never failed. A measure that names no family
  reads the theme's face, its text face or its code face, and depends on that family alike.
- **The document is asked first.** A family the document already has a face of, from the theme's
  stylesheet or the server's head, is not declared again: the server declared it whole.
