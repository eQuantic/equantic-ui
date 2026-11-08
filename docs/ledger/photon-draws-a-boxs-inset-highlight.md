- **2026-10-31 · Photon draws a box's inset highlight**: a Bug ([#1036](https://github.com/eQuantic/equantic-ui/issues/1036)).
  `BoxStyle.InsetHighlight` documented both targets, and only the web drew it, as an inset box-shadow one point
  down: no native code read the member, so a box that set it was glossy in a browser and flat on Photon. Photon
  draws it as the member says, a hairline along the top of the padding box, over the fill, inside the border and
  clipped to the corners, pinned by the reference backend's pixels beside the web's shadow. Proposed and archived
  through OpenSpec.
