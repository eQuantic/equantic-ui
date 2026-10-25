## Context

A face reaches a page today only through CSS the app writes, and reaches Photon only when the machine has it installed. `TypeStyle.Family` documents the gap itself: a named face is honoured by the measurer, the rasterizer and the raster cache alike, and a face the system has never seen falls back, reported by `FaceResolution`.

The first slice of #1038 put the text face in the theme (`TextFamily`, beside `MonoFamily`). Both are names, so the theme can say which face, and cannot say where it comes from.

## How Flutter solves it

The app's `pubspec.yaml` lists each family and its files, and gives each file its weight and its style:

```yaml
fonts:
  - family: Raleway
    fonts:
      - asset: fonts/Raleway-Regular.ttf
      - asset: fonts/Raleway-Italic.ttf
        style: italic
```

Every declared file is bundled with the app and registered with the engine at startup. A family is named by a string (`ThemeData.fontFamily`, `TextStyle.fontFamily`), and a package's family by the string and the package's name. `docs/FLUTTER-PARITY.md` has no row for fonts.

.NET has the same mechanism in MAUI: `ConfigureFonts` registers a font file the app embeds, and on iOS its embedded-font loader hands the file's bytes to `CTFontManagerRegisterGraphicsFont`.

## Decisions

**A family is a type that carries its files, and naming it is what ships it.**
- Flutter's list and its family string are two statements that must agree, and a mistyped string falls back in silence.
- Here the theme names a `FontFamily` object. The object is the name and the files, so there is nothing to keep in step.
- What is shipped is derived from what the theme names, not listed beside it: its text face, its code face and the face of every role.
- A family with no files is the one the machine has, written `new("Georgia")`. There is no conversion from a string: the string was the old shape, and the compiler points at every family named by one.
- `TypeStyle.Family` takes the type as well, because a role's face is set there, and a role's face is one a theme ships.

**The files travel inside an assembly, as embedded resources.**
- One mechanism serves every target. The server reads the bytes to serve them, and each native shell reads them to register them, from the same place.
- Nothing is copied into an app bundle, an Android asset folder or a `wwwroot`. A theme published as a library carries its faces in its own assembly, and an app that references it carries nothing more.
- MAUI's embedded fonts are the precedent.

**The file is the declaration, for an app.**
- An OpenType file already states its family (`name`), its weight (`OS/2`), the weight range of a variable font (`fvar`) and whether it is italic.
- The SDK reads them at build time with eqicon, which already reads the app's SVGs into `Vectors`. It writes a catalog with one typed member per family.
- A library declares its files by hand, with the same `FontFile` the catalog writes. That is the general mechanism, and the catalog only writes it for the app.
- A file eqicon cannot read fails the build, and so do two files that claim the same weight and style of one family. A build that warned would ship a theme that falls back in silence, which is the failure this exists to end.

**OpenType only.** CoreText, DirectWrite and Android's typefaces all read `.ttf` and `.otf`, and browsers do too. WOFF2 is smaller on the wire, but no native engine reads it without decoding it first. A second file per face, kept in step with the first, is the cost the catalog exists to avoid.

**The web addresses a file by its content.**
- The server reads each file once, hashes it, and serves it at `/_equantic/fonts/<hash>.<ext>` with an immutable cache policy.
- The URL names no assembly and no resource, so the route serves only what the theme and the catalog named. There is no path to walk.
- The theme's stylesheet gains one `@font-face` rule per file, with `font-weight` as the file's range, `font-style` from its italic flag, and `font-display: swap`. The rules are written once per theme, as the stylesheet is.
- A family the theme names is declared, and so is every family in the app's catalog, found through the `FontCatalog` attribute the catalog writes on its assembly. A rule costs nothing until text uses its family, because a browser fetches a face only when it sets text in it.

**The text face is preloaded, and nothing else.**
- The upright files of `TextFamily` set nearly every glyph of every page. A preload starts their fetch from the head, before the stylesheet asks for them.
- Every other face loads when text first uses it. Preloading a face a page does not use costs the bytes and delays the ones it does.
- The SDK decides this from what the theme already says, so there is no setting for it.

**Photon registers a family the first time text asks for it.**
- The text service receives the `FontFamily` object with the style, so it can register the files before it resolves the name. Then the platform's own matching, the bold and italic traits included, finds the face as it finds an installed one.
- Nothing has to list the families up front. A family a page names at a call site loads as the theme's do.
- Apple: the bytes become a `CGFont`, registered with `CTFontManagerRegisterGraphicsFont`.
- Windows: an in-memory font file loader feeds a font set, and that set becomes a font collection. Text in a family that ships files is formatted against that collection.
- Android: the bytes are written once to the app's cache directory and opened with `Typeface.CreateFromFile`, one typeface for each file, chosen by its italic flag.
- A file that cannot be read or registered is reported through `FaceResolution`, which is where an unresolved family is already reported.

**A page that measured text in a face still on its way rebuilds when it arrives.**
- The runtime measures text with a canvas: a code editor's column advance, a spreadsheet's header, a hover card's width. A canvas measures a declared face that has not loaded as its fallback, and the text is drawn in the face once it arrives, so the measurement and the drawing disagree.
- One function measures for every component, so the fix is there. When it measures in a face `document.fonts` says is declared and not loaded, it keeps the rebuild of the page whose build is in flight, and calls it once the face has loaded. Only the pages that measured with the wrong metrics rebuild, and each one once.

**Weights on Photon stay what they are.** A shell picks regular or bold, as it does for an installed family. Exact weights from a variable font's axis are a change to every shell's matching, not to how a face is loaded.

## Risks

- **The Windows interop is reached through vtable slots** (`IDWriteFactory3`, `IDWriteFactory5`, `IDWriteFontSetBuilder1`, `IDWriteInMemoryFontFileLoader`), and this machine cannot run it. A test on the Windows runner registers a face and measures text in it, so a wrong slot fails there rather than in an app.
- **The test font.** No third-party font file is committed, for its licence. The tests build small TrueType files in code (TestFont), with a family name nobody has installed and advances wide enough that text measured in it cannot be mistaken for the system face. The two files the server's and the shells' tests embed, 5 KB each, are files that builder wrote.
