## Context

`DirectWriteTextService` built one collection from every file the app ships (an in-memory font file loader,
a font set builder and `CreateFontCollectionFromFontSet`), rebuilt each time a file registered, and created a
text format against it with the family's name and the weight asked. DirectWrite then found the family by the
names it reads in the files, and the face by the weights it reads there. Every other target takes the
catalog's word instead (#1082): the web declares each file in an `@font-face` rule under the family's name,
at the catalog's weights, and Apple and Android open the file `ShippedFaces.FileFor` chooses.

Measured on the Windows runner (Windows Server 2025), with the shell as it was:

- Two files that declare the weight class 250 and carry 100 and 200 in their `STAT`, as Google's static Thin
  and ExtraLight of Inter do, measure two widths: DirectWrite tells them apart, which #1099 had expected it
  not to.
- A file the catalog weighs at 700 that declares 400 draws, in `Bold` and in `Black`, with a bold DirectWrite
  simulates over it: the bitmap comes out wider and heavier than the file's own drawing.
- A family the theme names "Brand Sans" whose file calls itself "eQuantic Test Face" is reported unresolved,
  and the text measures 88.7 in the system's face where the file sets 170.

## Goals / Non-Goals

**Goals:** Windows draws a weight of a shipped family from the file `ShippedFaces.FileFor` chooses, at the
weight the catalog gives it, under any name the theme gives the family, so the four targets decide alike.

**Non-Goals:** a variable file's weights on Windows, which is #1061 with Android's: DirectWrite still draws
a variable file at the named instance nearest the weight asked, as before. Laying a bold or a slant over a
family that lacks the weight or the slant asked stays as it is on each target: a browser and Android do,
Apple does not, and Windows does what DirectWrite did before with a weight the family does not ship.

## Decisions

- **How Flutter does it.** A family's files are declared with the weight each one sets (`weight: 100`
  beside the asset in `pubspec.yaml`), and the engine matches a weight against those declarations rather
  than against what the files say. The catalog is that declaration here, written by the build from the
  files or by a library by hand, and DirectWrite is handed the face it chose.
- **A collection per file.** A text format takes a family name and a collection, and DirectWrite matches
  among the faces the collection holds. A collection of one file holds that file's face alone, so the file
  `ShippedFaces.FileFor` chooses is the one drawn. Each collection is made when its file registers and kept
  for the life of the service; a format holds the collection it was made against. The collection of the
  whole family, rebuilt on every registration, goes.
- **The theme's name when the file carries it, the file's otherwise.** DirectWrite groups faces by the names
  their files carry, its weight, stretch and style family, which is not always the name the catalog reads
  (the typographic family, ID 16 of the `name` table), nor the one a library declares by hand. So the format
  names the family as the theme does when the chosen file's collection has that name, which keeps the right
  family where DirectWrite splits a variable file's instances into several, and as DirectWrite reads it in the
  file otherwise, its first name there. Apple and Android open the file and never ask for it by name.
- **A file is drawn at the weight DirectWrite reads in it.** DirectWrite gives a family whose heaviest face
  declares from 350 to 550 a bold face of its own beside it, the same face emboldened with its advances
  widened, at 700, and an oblique one beside an upright face with no italic: the measured bold above, which a
  collection of that one file still draws when asked for 700. So a static file asked for the weight its
  catalog gives it is asked for at the weight DirectWrite reads in that file, read from the face the family
  holds with no simulation, and drawn as the file draws, as a browser draws a face whose `@font-face` rule
  declares the weight asked. So is one the catalog weighs at 600 or more asked for a heavier weight, since a
  browser lays no synthetic bold over a bold face. A heavier weight than a lighter file sets is asked for as it
  is, and DirectWrite emboldens what the family lacks, as a browser does: a Regular file asked for `Bold`
  draws emboldened, measured.
- **The test faces are pinned.** A shell reads a face from the assembly it is embedded in, so the weight tests'
  faces are committed files, and a test holds each one to what `TestFont` builds, written again with
  `EQ_UPDATE_TEST_FONTS=1`.

## Risks / Trade-offs

- A family of N files makes N collections, where it made one, rebuilt N times. Each holds one file, and the
  bytes are held once, by the loader, as before.
- Android asks its engine for the weight over the file chosen too, and its engine lays a bold over a face
  asked for 200 above the weight the file declares: a file the catalog weighs at 700 that declares 400 would
  draw bolder there. It is #1109, and the scenario is held on Windows only until then.
