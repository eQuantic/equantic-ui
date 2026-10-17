- **2026-10-17 · The spreadsheet's engine leaves the vocabulary**: `SheetController`, its document,
  its editing, its keymap and its model lived in `eQuantic.UI.Primitives`, because `SheetSurface`
  took the controller and both realizers drove it directly, each turning a press into a cell with its
  own copy of the same prefix sums. They are an assembly of their own now, `eQuantic.UI.Sheet`, one
  type per file, which both SDKs add to an app as they add `eQuantic.UI.Code`, and `SheetSurface`
  takes an `ISheetSurfaceModel` that says what a host may tell the engine: a key, typed text, a
  pointer and the clipboard's events. The window the surface draws went with them (`SheetGrid`,
  handed over by the component as it builds), so what a press means is answered once: `PhotonHost`
  keeps only which surface a press began in, and the web half imports nothing of the engine. The
  breaks, for an app: `using eQuantic.UI.Sheet;` where it names the engine's types, `CopyTsv` and
  `PasteTsv` are `CopyText` and `Paste`, and a hand-built surface's `FirstRow`/`FirstCol` are
  `SheetController.Grid`. One behaviour moved with the protocol: on Photon a cut with no clipboard
  no longer clears the cells it could not copy, a cut writes the clipboard before it empties anything
  (on the code surface too, where a refused write had already deleted the text), and a press the
  system takes away reaches the engine as a new `PointerPhase.Cancel`, as does a browser press whose
  release the page never heard, so a fill nobody let go of pours nothing. And driving the dashboard's sheet to prove the move
  found an older defect: the web half focused the sheet before reading where it was pressed, the
  focus scrolled the scroller 127px, and the first press selected the cell five rows below. The point
  is read first now, and the focus takes no scroll. The spreadsheet's goldens and suites pass
  unchanged on both targets ([#185](https://github.com/eQuantic/equantic-ui/issues/185)). Proposed and
  archived through OpenSpec (`openspec/specs/spreadsheet`).
