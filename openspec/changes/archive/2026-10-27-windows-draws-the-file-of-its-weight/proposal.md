## Why

Closes #1099, a task under #1038 (the document and its faces come from the theme), under the epic #1035.
On the web, on Apple and on Android, a family that ships files is drawn from the file `ShippedFaces.FileFor`
chooses for the weight and the slant, the rule a browser follows (CSS Fonts §5.2), at the weight the
family's catalog gives each file, under the name the catalog gives the family (#1082). Windows does not take
that path. The shell hands DirectWrite every file of the family in one collection and asks for the family by
name and weight, so DirectWrite matches by the names and the weights it reads in the files.

#1099 expected Google's static Thin and ExtraLight of Inter, whose files both declare the weight class 250,
to draw from one file there. Measured on the Windows runner, they do not: two test files that declare 250
and carry 100 and 200 in their `STAT` measure two widths with the shell as it is. What the same run measured
instead, with the shell as it is:

- A file the catalog weighs at 700 that declares 400 draws with a bold DirectWrite simulates over it, in
  `Bold` and in `Black`, where the web and Apple draw the file as it is.
- A family the theme names otherwise than its file is reported unresolved and falls back to the system's
  face, where every other target sets it in its file.

## What Changes

For a developer using the SDK, nothing to write: a family's catalog decides on Windows as on every other
target which file sets a weight, at what weight, and under what name.

- **The Windows shell opens the file `ShippedFaces.FileFor` chooses.** Each shipped file is still handed to
  DirectWrite from memory, and it is made into a collection of its own: text in a family that ships files is
  set from the collection of the one file chosen for its weight and its slant.
- **The weight a file sets is the catalog's.** A static file asked for the weight the catalog gives it, or
  for a heavier one when the catalog weighs it 600 or more, is drawn as its file draws, whatever weight the
  file declares itself. A heavier weight than a lighter file sets is asked for as it is, as before.
- **A family is set in its files under any name.** The text format names the family as the theme does when
  the chosen file carries that name, and as DirectWrite reads it in the file otherwise.

The parts it reaches: the Windows shell (`DirectWriteTextService` and its DirectWrite bindings) and a
paragraph of the native framework's documentation (`ShippedFaces.FileFor`). The public surface does not
move, nor does the developer surface. Nothing an app wrote breaks. What an app draws changes on Windows
only, in the two cases above.
