- **2026-10-27 · Windows draws a shipped family from the file of its weight, as the other targets do**: a task
  under #1038 ([#1099](https://github.com/eQuantic/equantic-ui/issues/1099)). The Windows shell handed DirectWrite
  every file of a family in one collection and asked for the family by name and weight, and DirectWrite weighs a
  face by what its file says: Google's Thin and ExtraLight of Inter both say 250, so both weights drew from one
  file. Each shipped file is a collection of its own now, and text is set from the collection of the file
  `ShippedFaces.FileFor` chooses, under the name DirectWrite reads in it, a static file asked for its own weight
  at the weight DirectWrite reads there, so no bold is laid over a file the catalog weighs heavier than it says.
  The four targets draw a weight from the same file. Proposed and archived through OpenSpec (`typography`,
  `font-faces`).
