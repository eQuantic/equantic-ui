- **2026-11-11 · A pressed destination previews its pill**: the navigation bar and the rail pressed the pill
  into SurfaceSubtle, the colour B4 gives the hover ([#1254](https://github.com/eQuantic/equantic-ui/issues/1254)).
  The pill of every destination but the selected one is now pressed into the selection's own pill at 40%, on the
  web and on Photon, pinned by `PressedFillPlacementTests` and `PressedFillPlacementRealizerTests`. The hover the
  handoff asks for is not added: a box's hover is the box's own, so the pill would light only under the pointer
  and not across the destination ([#1456](https://github.com/eQuantic/equantic-ui/issues/1456)). The filter chip's
  half is [#1460](https://github.com/eQuantic/equantic-ui/issues/1460): the handoff's frames draw the unselected
  filter chip outlined, so its press is not fixed without its rest.
