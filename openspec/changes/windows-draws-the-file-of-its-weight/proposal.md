## Why

Closes #1099, a task under #1038 (the document and its faces come from the theme), under the epic #1035.
On the web, on Apple and on Android, a family that ships files is drawn from the file `ShippedFaces.FileFor`
chooses for the weight and the slant, the rule a browser follows (CSS Fonts §5.2), at the weight the catalog
gives each file (#1082). Windows does not take that path. The shell hands DirectWrite every file of the
family in one collection and asks for the family by name and weight, and DirectWrite weighs a face by what
its file says. Google's static Thin and ExtraLight of Inter both say 250 there, where their `STAT` says 100
and 200, which is what the catalog reads: DirectWrite sees one weight where there are two, and `Thin` and
`ExtraLight` draw from the same file on Windows.

## What Changes

For a developer using the SDK, nothing to write: a theme that ships Inter's Thin and ExtraLight sets each
one in its own file on Windows, as it already does on the web, on Apple and on Android.

- **The Windows shell opens the file `ShippedFaces.FileFor` chooses.** Each shipped file is still handed to
  DirectWrite from memory, and it is made into a collection of its own: text in a family that ships files is
  set from the collection of the one file chosen for its weight and its slant.
- **The weight a file sets is the catalog's.** A static file asked for the weight the catalog gives it is
  drawn as its file draws, whatever weight the file declares itself. A weight the family does not ship is
  asked for as it is, as before.
- **A family is set in its files under the name the files carry.** The text format names the family
  DirectWrite reads in the chosen file, so a theme's name for a family no longer has to be the one
  DirectWrite groups the file under, as on Apple and Android, which open the file itself.

The parts it reaches: the Windows shell (`DirectWriteTextService` and its DirectWrite bindings) and a
sentence of the native framework's documentation (`ShippedFaces.FileFor`). The public surface does not
move, nor does the developer surface. Nothing an app wrote breaks. What an app draws changes on Windows
only: a family whose files declare one weight for two draws each weight from its own file.
