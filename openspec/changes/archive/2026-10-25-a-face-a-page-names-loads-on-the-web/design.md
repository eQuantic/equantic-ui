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
- **One set of faces, a service.** `ServedFonts` becomes a singleton the render and the routes share, in
  place of a table beside the routes. It learns a file the first time a page or the runtime names it, and
  keeps it, so the route serves what has been named and nothing else.
- **The identity route is narrow.** `/_equantic/fonts/<assembly>/<resource>` reads only an assembly the
  app has loaded, never one it would have to load, and only a resource an eQuantic build embedded as a
  face, under `eQuantic.UI.Fonts/`, a `.ttf` or an `.otf`. An assembly may embed a font for something
  else, a PDF writer's, whose licence does not cover the web, and the route must not publish it because a
  browser asked. A family built by hand over another resource still loads on a page the server renders,
  where its author named the resource, and on Photon, and a page the browser builds says on the console
  why it fell back. Its redirect is not cached, so a new build's file is found under its new hash.
- **Whole or nothing in the browser.** The runtime adds a family's faces to the document at once, so a
  page that measured text in them rebuilds when they load (#1055's tracker), and when one fails to load it
  removes them all: the text falls back as a whole, as it does on Photon, rather than mixing a cut of the
  family with the platform's face.
- **The document is asked first.** A family the document already has a face of, from the theme's
  stylesheet or the server's head, is not declared again: the server declared it whole.
