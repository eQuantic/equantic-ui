# Proposal

Closes #1055, the second slice of #1038 (The document and its faces come from the theme), a Feature under #1035 (Any design system in C#, complete on the web and on native, with no CSS). The first slice drew the document from the theme. This one loads the faces the theme names.

## Why

A theme can name a face and no target can load one:

- `TypeStyle.Family`, `IAppTheme.TextFamily` and `IAppTheme.MonoFamily` are strings. Photon resolves them only against the faces the machine has installed, and the `Family` documentation says so: "registering one the system has never seen is a separate piece".
- On the web the face reaches the page only through CSS the app writes. Every app measured on 2026-10-24 (Cura, falei.pt, eQuantic's site, eQuantic Family and eQuantic.Auth's screens) loads its faces by hand, with `@font-face` rules or a provider's stylesheet, plus the preloads.

So a brand face is CSS on the web and the system face on Photon, on every machine that never installed it.

## What Changes

**A family is a type.**
- `FontFamily` names a family and carries the files it ships.
- A family with no files is one the machine has: `TextFamily => new("Georgia")`.
- `IAppTheme.TextFamily`, `IAppTheme.MonoFamily` and `TypeStyle.Family` take a `FontFamily`. Naming a family that ships files is all it takes to ship them.

**A file is declared in C#.** `FontFile` names one file of a family:
- the assembly it is embedded in, and its resource;
- its weight range, which is a variable font's whole axis;
- whether it is italic.

The files are OpenType (`.ttf`, `.otf`), which every target's text engine reads.

**An app declares nothing for its own faces.**
- A `.ttf` or `.otf` under the project's `Assets` folder is embedded in the app's assembly by the SDK.
- The SDK reads the family, the weights and the italic from the file itself and writes a typed catalog, so a theme writes `TextFamily => Fonts.AtkinsonHyperlegibleNext`.
- A file the SDK cannot read fails the build, and so do two files that claim the same face of one family.

**The web loads them.**
- The server serves each file of every family the theme names (its text face, its code face and every role's) and of every family in the app's catalog. The URL is addressed by the file's content and cached as immutable.
- The theme's stylesheet holds an `@font-face` rule for each file, with its weights and its style.
- The upright files of the text face are preloaded.

**Photon registers them.** The first time text asks for a family that ships files, the shell registers them with its text engine: CoreText on Apple, DirectWrite on Windows, the platform's typefaces on Android. Each file is read from the assembly it is embedded in, so nothing is added to an app's bundle. A family that ships no files resolves from the machine, as before.

### For a developer using the SDK

Put the face's files in `Assets/Fonts/` and name the family in the theme:

```csharp
public FontFamily? TextFamily => Fonts.AtkinsonHyperlegibleNext;
```

The `@font-face` rules, the preloads and the `AddStyles` block that loaded the faces are deleted.

A library that ships a theme with its own faces embeds the files and declares them:

```csharp
public static FontFamily Roboto { get; } = new("Roboto",
    new FontFile("Acme.Theme", "Fonts/Roboto-Variable.ttf", 100, 900),
    new FontFile("Acme.Theme", "Fonts/Roboto-Italic-Variable.ttf", 100, 900, Italic: true));
```

### Parts reached

- **Primitives:** `FontFamily`, `FontFile`, `FontCatalogAttribute`; the typed slots; the type merge.
- **Runtime:** the twins of the two types, the theme's twin and the bridge, and a page that measured text in a face still loading rebuilds when it arrives.
- **Web realizer:** the `@font-face` rules; the lowering reads a family's name.
- **Server:** the font route, the rules in the head, the preloads.
- **Photon:** registration in the Apple, Windows and Android shells.
- **SDKs:** the web and the native SDK embed the files under `Assets` and run eqicon over them.
- **eqicon:** reads an OpenType file and writes the catalog.
- **The email realizer:** reads a family's name.

The public surface moves in Primitives, Web and Native.Framework. `TypeStyle.Family`, `IAppTheme.TextFamily` and `IAppTheme.MonoFamily` change type. The developer surface gains the catalog's file and namespace properties, as the vectors have.

The migration line is: a theme declares `TextFamily` and `MonoFamily` as `FontFamily?`, and a family named by a string is written `new("Georgia")`. A face the app loaded with CSS moves its files to `Assets/Fonts/` and is named by its catalog member.

A theme that keeps `string? TextFamily` still compiles, because the interface answers the slot itself, and its face is ignored. So an upgrade searches its themes for `string? TextFamily` and `string? MonoFamily`.

## Not in this slice

- A family a page names at a call site that is neither the theme's nor in the app's catalog. Photon loads it, because it registers whatever text asks for, and the web falls back to the platform's face. That is #1056.
- A face from a provider (Google Fonts). That is #1057.
- WOFF2. The web could serve a smaller file, but every native engine reads OpenType, and one file per face on every target is the point.

## Capabilities

### New Capabilities

- `font-faces`: the faces an app ships, how they are declared, and how each target loads them.

### Modified Capabilities

- `typography`: a family is a type that carries its files.
