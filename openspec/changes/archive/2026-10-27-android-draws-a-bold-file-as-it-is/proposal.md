## Why

Closes #1109, a task under #1038 (the document and its faces come from the theme), under the epic #1035,
found reviewing #1099. From API 28 the Android shell asks its engine for the weight over the file
`ShippedFaces.FileFor` chooses (`Typeface.Create(face, weight, italic)`), and the engine, minikin, lays a fake
bold over a face asked for 600 or more and at least 200 above the weight the file declares in its `OS/2` table.
So a file the catalog weighs at 700 that declares 400 draws bolder on Android than on the web, on Apple and,
since #1099, on Windows, in `Bold` and in `Black`.

Measured on the emulator (API 36) with the shell as it is: the test face `WeightFace-400.ttf`, which declares
400, catalogued at 700 and asked for `Bold`, rasterizes through `AndroidTextService` byte for byte as the same
file catalogued at 400 and asked for `Bold`, with 7.5% more ink than the file drawn as it is.

## What Changes

For a developer using the SDK, nothing to write: a family's catalog decides on Android, as on every other
target, at what weight a file is drawn.

- **A file the catalog holds at the weight asked, or weighs bold, is drawn as it is.** From API 28 the Android
  shell asks for a shipped static file at the weight the typeface reads in the file (`Typeface.Weight`) when the
  file is asked for the weight its catalog gives it or a lighter one, or for a heavier one when the catalog
  weighs it 600 or more, so the engine lays no bold over it. A heavier weight than a lighter file sets is asked for as it is, and the engine emboldens what the
  family lacks, as before.
- **One rule for the two shells that ask.** The rule #1099 wrote into the Windows shell moves into the native
  framework, `ShippedFaces.IsDrawnAsItIs`, and both shells read it, so Windows and Android decide alike.

The parts it reaches: the Android shell (`AndroidTextService`), the Windows shell (`DirectWriteTextService`,
which reads the rule where it held its own copy) and the native framework (`ShippedFaces`). The public surface
gains one member on the native track, which is experimental: `ShippedFaces.IsDrawnAsItIs(FontFile, FontWeight)`.
The developer surface does not move. Nothing an app wrote breaks. What an app draws changes on Android from
API 28 only, in the case above. Below API 28 the shell still has regular and bold alone.
