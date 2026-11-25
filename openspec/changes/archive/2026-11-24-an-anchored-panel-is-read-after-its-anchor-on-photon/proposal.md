# Proposal

Closes #1868, a Bug under #1587.

## Why

Photon read an anchored panel in another place than the web. The web lowers a menu's, a select's, an editable
combobox's or a date picker's panel inside the anchor's markup, right after the anchor. Photon lays every panel out
in an overlay layer, and its semantics walk read the layers after the page, so whatever followed the anchor was read
before the panel. For a dialog panel, the web's dialog is the panel with the scrim's dismiss button outside it;
Photon's dialog stop was the whole layer, so the dismiss button was read inside the dialog. Found by the semantics
parity fixture on `case date-picker opened` (#1330 split it out).

## What Changes

- **Photon reads an anchored panel in place.** The walk finds the layer each anchored node's panel was laid out in
  (the layer is built in that node's deferred slot, so its root names the node's position as its parent), reads
  the anchor, then the layer: the scrim's dismiss button and the panel. The frame's walk does not read that layer
  again after the page. Flutter does the same: an `OverlayPortal`'s child is painted in the overlay, and its
  semantics are attached to the portal.
- **A dialog panel's stop is the panel**, after the scrim's dismiss button, no longer the whole layer.
- An anchored node the walk does not reach keeps its panel read after the page.
- The four semantics parity entries that waited on #1868 are removed.

A developer writes nothing different. No public surface and no developer surface moves.

## Impact

- `eQuantic.UI.Native.Components`: the semantics walk (`SemanticsTree.Collect`, the visitor's anchored arm).
- Every shell reads the new order through the tree it already walks: VoiceOver, iOS's reader and TalkBack hear an
  open menu, select, combobox list or calendar right after what opened it.
