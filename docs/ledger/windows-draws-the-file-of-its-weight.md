- **2026-10-27 · Windows draws a shipped family from the file of its weight, as the other targets do**: a task
  under #1038 ([#1099](https://github.com/eQuantic/equantic-ui/issues/1099)). The Windows shell handed DirectWrite
  every file of a family in one collection and asked for the family by name and weight, so DirectWrite decided by
  what it reads in the files, where every other target follows the family's catalog. Measured on the Windows
  runner, the case #1099 expected does not occur: DirectWrite tells Inter's Thin and ExtraLight apart, whose files
  both declare 250. Two others did: a file the catalog weighs at 700 that declares 400 drew with a bold
  DirectWrite simulates, and a family the theme names otherwise than its file fell back. Each shipped file is a
  collection of its own now, text is set from the collection of the file `ShippedFaces.FileFor` chooses, under
  the theme's name when the file carries it, and a static file the catalog holds at the weight asked, or weighs
  bold, is asked for at the weight DirectWrite reads in it. Proved on the Windows runner three ways: the change,
  the shell as it was and the change without the catalog's weight. Proposed and archived through OpenSpec
  (`typography`, `font-faces`).
