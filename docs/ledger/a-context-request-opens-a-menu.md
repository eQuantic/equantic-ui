- **2026-11-15 · A node asks for its menu**: `ContextTarget` wraps any node and hears a person ask for its menu,
  by a secondary click (a Control-click on a Mac), a finger held still for 500 ms, or the menu key or Shift+F10
  with the focus inside, the innermost target answering with the point in its own box. `Anchored.At` opens a
  panel at that point, and `ContextMenu` is the composition over the `Menu`'s own panel, which both now draw from
  one place. The browser's menu stays out only where a target answers; Photon's host counts the hold and takes the
  keys, and the macOS, Windows, Android and iOS shells deliver the secondary click and the menu key
  ([#1531](https://github.com/eQuantic/equantic-ui/issues/1531)). Proposed and archived through OpenSpec
  (`openspec/specs/context-requests`).
