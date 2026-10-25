# Tasks

## 1. A family is a type

- [x] 1.1 `FontFamily`, `FontFile` and `FontCatalogAttribute` in Primitives, one type per file, with value equality and no conversion from a name
- [x] 1.2 `TypeStyle.Family`, `IAppTheme.TextFamily` and `IAppTheme.MonoFamily` take a `FontFamily`, and every reader follows: the type merge, the face rule, the web lowering, the theme's stylesheet, the bridge, the email and the three shells' text services
- [x] 1.3 The runtime's twins: `FontFamily` and `FontFile`, the style's and the theme's family, the bridge, the interface defaults and the design system's generated twin
- [x] 1.4 Proof: the Compiler, Conformance, Web, Server and Native suites and the runtime's, the transpiled twins regenerated and read, and the bridge's payload byte-identical for a theme that ships no files

## 2. An app's faces are read from their files

- [x] 2.1 A small TrueType file built in code for the tests, with a family no machine has
- [x] 2.2 eqicon reads an OpenType file's family, weights and italic, and refuses a file it cannot read
- [x] 2.3 eqicon writes the catalog through a writer of its own, one member per family, with the `FontCatalog` attribute on its assembly
- [x] 2.4 The web and the native SDK embed every `.ttf` and `.otf` under `Assets` and run eqicon over them, cold builds included, and the developer surface's baseline gains the catalog's properties
- [x] 2.5 Proof: the reader against built files (upright, italic, a weight range, not a font, two files claiming one face), and an app built by the SDK whose catalog and resources are read back

## 3. The web serves and declares them

- [x] 3.1 The `@font-face` rules for a set of families, written by the web realizer from each file's URL
- [x] 3.2 The server's registry: the theme's families and the catalog's, each file read once and hashed; the font route; the rules in the theme's stylesheet; the text face's preloads in the head
- [x] 3.3 A page that measured text in a face still loading rebuilds when it arrives
- [x] 3.4 Proof: a served page's head and the route's answer (bytes, type, cache policy, 404), a page whose theme ships no files whose head is unchanged, and the rebuild against a face that loads late

## 4. Photon registers them

- [x] 4.1 The shared part: a file's bytes read from its assembly, each file registered once, a failure reported as an unresolved family
- [x] 4.2 Apple: `CTFontManagerRegisterGraphicsFont`, before the family is matched
- [x] 4.3 Windows: an in-memory font file loader, a font set and a collection for the families that ship files
- [x] 4.4 Android: the files written to the cache directory and opened as typefaces
- [x] 4.5 Proof: text measured in the built face on the Mac and on the Windows runner measures as that face and reports nothing; a missing resource is reported; the Android shell builds

## 5. The real thing

- [x] 5.1 A shipped face in a probe app in a browser: the head, the served file and the computed face read back, with the face loaded and the text measured in it
- [x] 5.2 The same face in the macOS Photon shell, looked at

## 6. Documentation

- [x] 6.1 The wiki's DesignSystem page in every language it is written in: shipping a typeface, on the wiki branch named like the pull request's
- [x] 6.2 `TypeStyle.Family`'s documentation says how a face is shipped, not that it cannot be
- [x] 6.3 One ledger entry citing #1055, as a file of its own under `docs/ledger/`
- [x] 6.4 Archive this change in the same pull request
