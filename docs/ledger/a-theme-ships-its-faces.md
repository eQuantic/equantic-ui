- **2026-10-25 · A theme ships its faces**: the second slice of #1038, under the epic of any design
  system in C# with no CSS (#1035). A family is a type: `FontFamily` names it and carries its files,
  each a `FontFile` (the assembly it is embedded in, its resource, its weight range, its italic), and
  `TypeStyle.Family`, `IAppTheme.TextFamily` and `IAppTheme.MonoFamily` take it, the string going with
  no conversion beside it. An app declares nothing for its own faces: both SDKs embed every `.ttf` and
  `.otf` under `Assets`, and eqicon reads each file's family, weights and italic into a typed `Fonts`
  catalog, stopping the build on a file it cannot read (EQ4005) or two that claim one face (EQ4006).
  The web serves every file of the theme's families and of the app's catalog by the hash of its bytes,
  immutable, declares each with an `@font-face` rule in the theme's stylesheet and preloads the text
  face's upright files; a page that measured text in a face still loading rebuilds when it arrives.
  Photon registers a family's files with each shell's text engine the first time text asks for it:
  CoreText's font manager on Apple, an in-memory font collection on Windows, typefaces from the cache
  on Android ([#1055](https://github.com/eQuantic/equantic-ui/issues/1055)). A family a page names
  outside the theme and the catalog loads on the web in #1056, and a face from a provider in #1057.
  Proposed and archived through OpenSpec (`openspec/specs/font-faces`, a new spec, and `typography`).
  Its review found eqc deciding a params argument by its type, so a collection that was not an array
  arrived as one element, and recursing until the stack overflowed on a C# 13 params collection listed
  at a call: both are read from the bound tree now.
