- **2026-11-28 · A box tiles a grid, a vector drawing or an image behind its content, with what CSS gives a page, on
  Photon as on the web**: `BoxStyle.Pattern` took only a 1dp grid, so a brand's texture behind a surface could not be
  written ([#1937](https://github.com/eQuantic/equantic-ui/issues/1937), the first slice of #1042).
  `BoxStyle.Patterns` now holds a list from a closed set, `GridPattern`, and `DrawingPattern` and `ImagePattern` under
  `ArtworkPattern`, with a visitor every realizer implements. The bar is the CSS backgrounds model: repetition per
  axis, spaced and fitted, an anchor with an offset, a size in dp, a fraction or a fit, the area the tiles are placed
  in and the one they are clipped to, a window attachment, and several layers.
  The web writes each layer as a `background-image` entry, a drawing as an SVG data URL encoded by one table on both
  sides. A layer that differs by mode or contrast picks its URL through custom-property toggles, set under the rules
  that already pick `Image.DarkSource`'s artwork. Photon composites each tile once and draws each layer in one
  `TiledTexture` command that samples it with repeat addressing, so no seam shows at any scale.
  Measured in Chrome 157, Firefox 159 and iOS 27's Safari, the engines disagree on snapping. Chromium snaps neither the
  period nor the phase, Gecko the phase, and WebKit both. So Photon keeps the exact geometry: a rounded 866 px period
  would drift 2.25 px from Chromium by the tenth 330 dp tile at 2.625. All three paint `fixed` as `scroll` under a
  transform, and iOS anchors it to the document.
  The desk sample found a defect that the checks of the stylesheet's text passed: the contrast toggles' default came
  after the query that flips them, and won. A test now reads the cascade's order. A grid in a bordered or rounded box
  now starts and stops where the web's does ([#1939](https://github.com/eQuantic/equantic-ui/issues/1939)).
  Two defects outside the slice were found and filed. Photon's `Image` ignores its `DarkSource`
  ([#1945](https://github.com/eQuantic/equantic-ui/issues/1945)), and a Photon app gets no `Vectors` catalog from its
  SVG assets ([#1946](https://github.com/eQuantic/equantic-ui/issues/1946)). Proposed and archived through OpenSpec
  (`openspec/specs/box-paint`).
