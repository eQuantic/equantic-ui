- **2026-11-26 · A box's inset highlight is drawn on Photon, the padding box's top row as on the web**:
  `BoxStyle.InsetHighlight` documented both targets, and only the web drew it (an `inset 0 1px 0` box-shadow entry).
  No native project read the member, so a box that set it had its highlight in the browser and none on Photon. Photon
  now fills one dp row along the top of the box's padding box, past the border, clipped to its rounded corners, over
  the background layers and under the border. A test pins the web's shadow entry and Photon's pixels on the same boxes
  ([#1036](https://github.com/eQuantic/equantic-ui/issues/1036)). Proposed and archived through OpenSpec
  (`openspec/specs/box-paint`).
