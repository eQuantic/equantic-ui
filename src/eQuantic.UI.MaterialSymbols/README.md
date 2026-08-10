# eQuantic.UI.MaterialSymbols

Material Symbols for eQuantic.UI — 16,284 glyphs as write-once `IconGlyph` data, generated from the
Iconify `material-symbols` set.

Google ships Material Symbols as a variable FONT with `FILL`, `wght`, `GRAD` and `opsz` axes, and
the three cuts (outlined, rounded, sharp) are what those axes resolve to. A font is not something a
GPU display list can draw, so this package carries the resolved PATHS instead: the cut lives in the
icon's name — `Home`, `HomeRounded`, `HomeSharp` — and the same catalog serves the web realizer as
inline SVG and the native atlas as glyph geometry. That is also why there is no `FILL` axis here;
`X` and `XOutline` are two entries.

This is the largest set the framework packages, which is worth knowing before you take it: the
assembly is around 13 MB. Nothing near that reaches a browser — eqc inlines only the glyphs a page
actually names — but it is real weight in a build. If you use a handful of icons and have a choice,
a smaller pack (Lucide, Heroicons, Phosphor, Iconoir, Bootstrap Icons) costs less to carry.

## Installation

```xml
<PackageReference Include="eQuantic.UI.MaterialSymbols" Version="..." />
```

## Use

```csharp
using eQuantic.UI.MaterialSymbols;

Icon(MaterialSymbolsIcons.PlayArrowRounded)
Icon(MaterialSymbolsIcons.ExpandMore, size: 20)
```

The names are the Iconify ones in PascalCase, so `material-symbols:play-arrow-rounded` is
`PlayArrowRounded`.

## Regenerating

The catalog has exactly one writer — never hand-edit `MaterialSymbolsIcons.cs`:

```bash
node scripts/generate-icons.mjs material-symbols
```
