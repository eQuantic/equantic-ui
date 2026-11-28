## Why

Closes #1955 and #1956, two Bugs under #1587, found while designing #1938. Both are one family: a box's gradient on
Photon drew differently from the CSS the web writes for the same box. Measured in Chromium 156, each CSS gradient drawn
into a canvas through an SVG image and read back:

- **The corner line (#1955).** The web writes `linear-gradient(to bottom right, …)`. CSS's corner keywords turn the
  gradient line until it is perpendicular to the diagonal through the other two corners, so the gradient's middle passes
  through them (CSS Images, "magic corners"). Photon ran the line from corner to corner. On a 400 by 240 box, black into
  white, the browser reads 127 at the top-right corner and 96 at (300, 0). Photon put the top-right corner 73.5% of the
  way along the line. `to bottom left` is the same, mirrored, and a square box is the same on both targets.
- **The mix of the stops (#1956).** A CSS gradient mixes its stops premultiplied: each colour weighed by its alpha, and
  the mix divided by the mixed alpha. Photon mixed each channel on its own. Over nothing, red into transparent reads
  (255, 0, 0, 127) at its middle in the browser and (128, 0, 0, 128) on Photon, because CSS's `transparent` is
  transparent black. `rgba(0, 0, 255, 0.5)` into red reads (170, 0, 85, 192) where Photon read (128, 0, 128, 191).
- **An SVG paint server is not a CSS gradient.** The same browser mixes an SVG `linearGradient`'s stops straight. Red
  into transparent black reads (129, 0, 0, 127) at its middle. An artwork's runs are SVG on the web, so they keep the
  straight mix.

The comparison over nothing is deliberate. Over white the two targets differ for any translucent fill, a gradient or
not, because Photon blends in linear light and the browser in sRGB. That difference is outside this change.

## What Changes

Nothing an app writes changes. A box's gradient, its glow and a text's gradient now draw on Photon as the browser draws
the CSS the web writes for them.

- **A corner's line is CSS's.** `GradientLine.Across` places a box's gradient line: a side edge to edge, a corner along
  (h, w) through the centre, its half the half-diagonal's projection, w·h / √(w² + h²). The box's gradient and the
  text's gradient both take it.
- **A gradient names how its stops mix.** `Paint.Linear` and `Paint.Radial` take a `GradientInterpolation`: `Straight`
  or `Premultiplied`.
  - The box's gradient, its glow and the text's gradient pass `Premultiplied`.
  - An artwork's runs pass `Straight`.
  - The CPU (`Paint.ColorAt`) and the shader (`gradientColor`, carried in the uniform block's spare `flags.w`) mix
    alike, and the shaders are regenerated from the one Slang source.

Parts reached: the native engine's paint, uniform block and shader, and Photon's components. The experimental native
surface moves: `GradientInterpolation` and `Paint.Interpolation` join it, and `Paint.Linear` and `Paint.Radial` gain
the interpolation as a required parameter, the old shapes removed. Code that built a gradient paint itself names the mix
its target draws: `Premultiplied` for a CSS gradient, `Straight` for an SVG one.

The change follows docs/PRINCIPLES.md: each target's rule is measured rather than assumed, and the paint states its mix
rather than carrying a default one call site would have to override.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `box-paint`: a corner's gradient line is CSS's on both targets, and a gradient's stops mix as the web's CSS mixes
  them, while an artwork's run mixes as an SVG paint server does.

## Impact

- `AGradientRunsAsTheBrowserDrawsItTests` holds every measured value, on the Reference backend, Metal and Vulkan:
  - the wide box, the tall box and `to bottom left`;
  - the fade into transparent and the half-transparent stop beside an opaque one;
  - an artwork's straight run.

  On main every box case fails on all three targets. Mixing the artwork premultiplied too fails the artwork's case.
  Fixing the CPU and keeping main's shaders fails the GPU targets.
- `GradientShimmerTests` pins the new line on a 120 by 40 box. The skeleton's shimmer goldens change: its glint fades
  into transparent without the grey band the straight mix drew, as the web's CSS draws it.
