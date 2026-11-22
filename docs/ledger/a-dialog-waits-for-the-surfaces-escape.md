- **2026-11-09 · A dialog's Escape waits for the code editor or the spreadsheet in it**: a bug under #419
  ([#1181](https://github.com/eQuantic/equantic-ui/issues/1181)). Inside a dismissible Dialog, BottomSheet or
  Drawer, Escape on an open completion list closed the whole layer, and what was typed went with it; measured in
  Chromium on the dashboard sample, where the same editor on the page closed only the list. Both targets asked
  the page's bindings first, the web on the window in the capture phase and Photon's host before its code target,
  so the editor and the sheet never heard an Escape a layer around them binds, though their keymaps claim it
  while something is open. `ICodeSurfaceModel` and `ISheetSurfaceModel` now say whether Escape is theirs
  (`ClaimsEscape`, answered without doing anything), the web's shortcut controller asks the focus before a
  binding fires (`eq:takes-key-first`, which a text door answers from its model), and Photon's `KeyDown` asks its
  code and sheet targets. An app's own chords still answer before a focused editor, and Android's back still
  closes the layer over an edit. Proved with tests that failed first on both targets and in Chromium. Proposed
  and archived through OpenSpec (`keyboard-shortcuts`).
