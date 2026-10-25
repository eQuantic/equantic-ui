- **2026-10-25 · Text is set in each of the nine weights, on the web and on Photon**: a task under #1038
  ([#1082](https://github.com/eQuantic/equantic-ui/issues/1082)). `FontWeight` names the nine weights OpenType
  defines, `Thin` (100), `ExtraLight` (200), `Light` (300) and `Black` (900) appended to the five, so
  `GoogleFont` takes all nine. The runtime reads a weight's number from a table generated from the C# enum
  (`FontWeightNumbers`), where a hand-written switch drew the new ones at 400. CoreText is asked for the
  weight itself, its weight trait, with the slant, where it was asked for one bold trait at 600 and above,
  and Android for the weight from API 28. A family that ships files is drawn from the file of its weight and
  slant, opened directly and chosen as a browser chooses (`ShippedFaces.FileFor`), since CoreText's match by
  weight picked neighbouring files of Inter, measured. eqicon's catalog reads a static face's weight from its
  `STAT`, where Google's Thin and ExtraLight of Inter, which both carry the weight class 250, say 100 and 200.
  Proved in a browser, in a Photon frame drawn on macOS with no window and on the Android emulator. Proposed and
  archived through OpenSpec (`typography`).
