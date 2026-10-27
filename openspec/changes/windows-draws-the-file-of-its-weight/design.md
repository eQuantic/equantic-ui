## Context

`DirectWriteTextService` built one collection from every file the app ships (an in-memory font file loader,
a font set builder and `CreateFontCollectionFromFontSet`), rebuilt each time a file registered, and created a
text format against it with the family's name and the weight asked. DirectWrite then found the family by
name, among the families the files' name tables group them into, and the face by weight, among the weights
the files declare in `OS/2`. Both are the file's word, where every other target takes the catalog's (#1082):
the web declares each file in an `@font-face` rule at the catalog's weights, and Apple and Android open the
file `ShippedFaces.FileFor` chooses.

## Goals / Non-Goals

**Goals:** Windows draws a weight of a shipped family from the file `ShippedFaces.FileFor` chooses, so the
four targets draw it from the same file.

**Non-Goals:** a variable file's weights on Windows, which is #1061 with Android's: DirectWrite still draws
a variable file at the named instance nearest the weight asked, as before. Laying a bold or a slant over a
family that lacks the weight or the slant asked stays as it is on each target: a browser and Android do,
Apple does not, and Windows does what DirectWrite did before with a weight the family does not ship.

## Decisions

- **How Flutter does it.** A family's files are declared with the weight each one sets (`weight: 100`
  beside the asset in `pubspec.yaml`), and the engine matches a weight against those declarations rather
  than against what the files say. The catalog is that declaration here, written by the build from the
  files, and DirectWrite is handed the face it chose.
- **A collection per file.** A text format takes a family name and a collection, and DirectWrite matches
  among the faces the collection holds. A collection of one file holds that file's face alone, so the file
  `ShippedFaces.FileFor` chooses is the one drawn. Each collection is made when its file registers and kept
  for the life of the service; a format holds the collection it was made against. The collection of the
  whole family, rebuilt on every registration, goes.
- **The family's name is the file's.** The format names the family as DirectWrite reads it in the chosen
  file's collection, its first name there, not as the theme names it. DirectWrite groups faces by the names
  their files carry, its weight, stretch and style family, which is not always the name the catalog reads
  (the typographic family, ID 16 of the `name` table), and a mismatch made a family that registered fall
  back. Apple and Android open the file and never ask for it by name.
- **A file is drawn at the weight DirectWrite reads in it.** DirectWrite gives a family whose heaviest face
  declares from 350 to 550 a bold face of its own beside it, the same face emboldened, its advances widened,
  at 700, and an oblique one beside an upright face with no italic. So in a collection of one Regular file, a
  weight asked for at 700 draws that simulated bold. A static file asked for the weight its catalog gives it
  is therefore asked for at the weight DirectWrite reads in that file, read from the face the family holds
  with no simulation, and drawn as the file draws, as a browser draws a face whose `@font-face` rule declares
  the weight asked; so is one the catalog weighs at 600 or more asked for a heavier weight, since a browser
  lays no synthetic bold over a bold face. A heavier weight than a lighter file sets is asked for as it is,
  and DirectWrite emboldens what the family lacks, as a browser does.
- **The theme's name when the file carries it.** DirectWrite can group the instances of a variable file into
  more than one family, so the format names the family as the theme does when the file's collection has it,
  and as DirectWrite reads it in the file otherwise.
- **The test faces are pinned.** A shell reads a face from the assembly it is embedded in, so the weight tests'
  faces are committed files, and a test holds each one to what `TestFont` builds, written again with
  `EQ_UPDATE_TEST_FONTS=1`.

## Risks / Trade-offs

- A family of N files makes N collections, where it made one, rebuilt N times. Each holds one file, and the
  bytes are held once, by the loader, as before.
- Android asks its engine for the weight over the file chosen too, and its engine lays a bold over a face
  asked for 200 above the weight the file declares: a file the catalog weighs at 700 that declares 400 would
  draw bolder there. No font measured here does that, since a `STAT` that moves a weight moves it below the
  weight class (Inter's 250). It is #1109, and the scenario is held on Windows only until then.
