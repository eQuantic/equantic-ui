- **2026-11-24 · An anchored panel is read right after its anchor on Photon, and a dialog panel's stop is the
  panel**: Photon's semantics walk read every overlay layer after the page, so an open menu, select, combobox list
  or calendar came after whatever followed its anchor, and a dialog panel's stop was the whole layer, with the
  scrim's dismiss button inside it. The walk now reads each anchored node's layer in place, after the anchor, as the
  web lowers the panel and as Flutter attaches an `OverlayPortal`'s child to the portal, and a dialog panel's stop
  is the panel, after the dismiss button. The four semantics parity entries of the open date picker are gone
  ([#1868](https://github.com/eQuantic/equantic-ui/issues/1868)). Proposed and archived through OpenSpec
  (`openspec/specs/native-accessibility`).
