- **2026-11-23 · A modal layer in a hidden adaptive arm lets go of the keyboard**: the web mounts every arm of an
  adaptive node and shows one, so a drawer left open in the compact arm kept its trap when the window widened, and
  held the keyboard in a layer nobody could see. The browser's focus trap controller now asks the gates around a
  layer when the Tab or the focus arrives, as the shortcut dispatcher asks them around a binding, and the topmost
  layer on screen owns the keyboard ([#1185](https://github.com/eQuantic/equantic-ui/issues/1185)). Proposed and
  archived through OpenSpec (`openspec/specs/modal-focus`).
