## Why

Closes #1069, a bug under #1038 (the document and its faces come from the theme), under the epic #1035.
Copilot's third review of #1062, which shipped the faces a theme names (#1055), found three defects in code
the earlier rounds had read, and #1062 merged before their fixes were pushed:

- The theme bridge sends a family by its name alone, so the browser rebuilds a shipped family with no
  files: a component reading `context.Theme.TextFamily.Files.Count` gets two on the server and none after
  hydration, and the family stops equalling the catalog's.
- A call eqc reads from its syntax keeps one argument a parameter when it reorders named arguments, so the
  elements a call lists for a params parameter after a named argument are dropped:
  `new FontFamily(Name: "Brand", regular, italic)` loses its italic file in the browser.
- Android's font cache throws when the cache is full or out of reach, before the loading catch, so text
  measurement fails where the family should fall back, and a write cut short leaves a partial file under
  the face's name, which a later launch opens.

## What Changes

For a developer using the SDK:

- A family the theme ships is the same family in the browser as on the server, its files included, for
  the text face, the code face and every role. A family with no files crosses as its name, so a theme of
  platform faces sends the payload it always sent. A file's names are text no rule vets, so a `<` and a
  control character are written as their escapes inside the script element the payload is written into.
  The generated theme, the client's other producer of one, writes the files the same way.
- A call that names an argument and then lists the elements of a params parameter passes every element,
  in a call, an app type's constructor and a vocabulary type's constructor alike.
- On Android, a face is cached whole or not at all: written beside its name and moved onto it, and a cache
  that cannot take it is a face that did not register, so its family falls back and is reported as any
  other.

The parts it reaches: the web's theme bridge (`ThemeBridge`, `materializeTheme`) and the design system's
generator, eqc (`BoundArguments`), and the Photon framework and the Android shell
(`ShippedFaces.CacheFile`). The public surface moves by one addition on the native track,
`ShippedFaces.CacheFile`. The developer surface does not move, and nothing breaks.
