- **2026-11-28 · A box's gradient on Photon runs along CSS's line and mixes its stops as the browser does**: Photon ran
  a corner's gradient from corner to corner, where CSS's corner keywords turn the line until its middle passes through
  the other two corners, and it mixed the stops straight, where a CSS gradient mixes them premultiplied. Measured in
  Chromium 156 on a 400 by 240 box, black into white toward the bottom right reads 127 at the top-right corner, which
  Photon put 73.5% of the way along. Red into transparent reads (255, 0, 0, 127) at its middle over nothing, where
  Photon read (128, 0, 0, 128). `GradientLine.Across` places the box's and the text's line, and `Paint` names its
  `GradientInterpolation`, which the CPU and the shader mix alike: premultiplied for the box's gradient, its glow and
  the text's gradient, straight for an artwork's run, which is an SVG paint server on the web and mixes straight there
  too. The skeleton's glint now fades without the grey band the straight mix drew
  ([#1955](https://github.com/eQuantic/equantic-ui/issues/1955),
  [#1956](https://github.com/eQuantic/equantic-ui/issues/1956)). Proposed and archived through OpenSpec
  (`openspec/specs/box-paint`).
