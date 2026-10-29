## Context

`AndroidTextService` opens each shipped file as a typeface of its own (`Typeface.CreateFromFile`, #1055) and
draws a style from the one `ShippedFaces.FileFor` chooses (#1082). From API 28 it asks for that typeface at the
weight the style names, with `Typeface.Create(face, weight, italic)`. The typeface reads its weight in the
file's `OS/2` table, and minikin, Android's font matcher, fakes a bold when the weight asked is 600 or more and
at least 200 above that weight. The catalog gives a file the weight it sets, which is usually the weight its file
declares and not always: the build reads a static face's weight in `STAT` before its weight class, and a library
may catalogue a file by hand.

Measured on the emulator (API 36), with a probe that rasterizes four rows through `AndroidTextService` and
compares each with row 1 byte for byte, the face being `WeightFace-400.ttf`, which declares 400, and the shell
as it is:

- row 1, catalogued at 400 and asked for `Regular`: the file as it draws
- row 2, catalogued at 700 and asked for `Bold`: the raster of row 3
- row 3, catalogued at 400 and asked for `Bold`: a fake bold, 7.5% more ink than row 1
- row 4, catalogued at 700 and asked for `Black`: the raster of row 3

## Goals / Non-Goals

**Goals:** Android draws a static file the catalog holds at the weight asked, or weighs bold, as the file draws,
as the web, Apple and Windows do.

**Non-Goals:** a variable file's weights on Android, which is #1061. Below API 28 a typeface cannot be asked for
a weight, and the shell keeps regular and bold. Laying a bold over a family that lacks the weight asked stays as
it is: Android's engine does, as a browser does.

## Decisions

- **How Flutter does it.** A family's files are declared with the weight each one sets (`weight: 700` beside
  the asset in `pubspec.yaml`), and the engine matches a weight against those declarations rather than against
  what the files say. The catalog is that declaration here, and #1099 made Windows follow it. Android is the
  other shell whose engine is asked for a weight over the file chosen.
- **The rule lives once, in the framework.** `ShippedFaces.IsDrawnAsItIs(file, weight)` answers whether the
  file `FileFor` chose is drawn as it is: a static file asked for its catalog's weight or a lighter one, or one
  the catalog weighs 600 or more asked for a heavier one, since a browser lays no synthetic bold over a face its
  rule declares bold. The Windows shell held the same condition inline, over the same weight (`asked` is the
  weight of the style until then), and reads this one now, so the two engines that are asked cannot drift.
  Apple opens the file and asks no weight over it, and the web declares the file at the catalog's weights in its
  `@font-face` rule, so neither reads it.
- **The weight asked is the one the typeface reads in its file.** For a file drawn as it is, the shell asks for
  `Typeface.Weight` of the typeface opened from that file, so minikin compares the weight asked with the file's
  own, finds them equal and lays nothing over it. Any other file is asked for the weight of the style: a Regular
  file asked for `Bold` still draws a fake bold, measured.

## Risks / Trade-offs

- A file the catalog weighs lighter than it declares, one that declares 700 catalogued at 400, is now asked for
  700, its own weight. minikin draws it as it is either way, since it fakes a bold only above the file's weight.
