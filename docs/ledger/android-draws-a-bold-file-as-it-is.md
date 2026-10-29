- **2026-10-27 · Android draws a shipped file the catalog weighs bold as the file draws, with no fake bold**: a
  task under #1038 ([#1109](https://github.com/eQuantic/equantic-ui/issues/1109)), found reviewing #1099. From
  API 28 the Android shell asked its engine for the style's weight over the file `ShippedFaces.FileFor` chooses,
  and minikin lays a fake bold over a face asked for 600 or more and 200 above the weight its file declares, so a
  file the catalog weighs at 700 that declares 400 drew bolder there than on every other target. The rule #1099
  wrote into the Windows shell moved into the framework, `ShippedFaces.IsDrawnAsItIs`, and both shells read it: a
  static file asked for its catalog's weight or a lighter one, or weighed bold and asked for a heavier one, is asked
  for at the weight its typeface reads in the file. Proved on the emulator three ways, rasterizing through
  `AndroidTextService` and comparing byte for byte: the change, the shell as it was and the change without the
  clause for a bold file. Proposed and archived through OpenSpec (`typography`).
