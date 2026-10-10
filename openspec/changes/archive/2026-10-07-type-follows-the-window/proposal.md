# Proposal

Closes #652, a sub-issue of #190 (Flutter parity: the gaps that are work). Met by falei.pt on
0.2.0-preview.60: the portal's heading and the panel headings.

## Why

A handoff sizes its headings the way CSS does: `font-size: clamp(34px, 4.2vw, 54px)`, a size that
grows with the window between a floor and a ceiling. A `TypeStyle` has one size in points, so an
app samples the clamp at one width per window class and switches between three `Text`s with an
`AdaptiveNode`. Every arm renders into the document, the heading is three headings to a search
engine and to assistive tech, and the size jumps at each class boundary instead of following the
window.

## What Changes

- **A size can follow the window.** `TypeStyle.WithFluidSize(min, percentOfWindow, max)` returns the
  style with a `FluidSize`: `percentOfWindow` percent of the window's width, never under `min` nor
  over `max`, in dp. `Size` and `LineHeight` then hold the ceiling, which is what a target that knows
  no window (an email) sets. The line box keeps the style's ratio at every size.
- **`TypeStyle.AtWindow(width)`** resolves a fluid size to dp for a window that wide, with the line
  box and the tracking following at the style's own ratio (a heading tracked at -0.035em stays
  -0.035em at every size); a style already in dp comes back unchanged.
- **`WithSize` gives a fluid size way.** A size given there is a size in dp: the result carries no
  `Fluid`, which is also what a run inside a paragraph keeps (a run takes only the size).
- **The web** writes `font-size: clamp(min, Nvw, max)`, a unitless `line-height` and an `em`
  `letter-spacing` (the style's own ratios), inline for a style override and in the role class for a theme's role, from the C# realizer
  and its TypeScript twin alike. A size in dp lowers exactly as before.
- **Photon** measures and paints a fluid style at the window the frame lays out against
  (`LayoutContext.WindowWidth` when measuring, `MotionScope.ViewportW` when painting), so the raster
  is the box layout made.

## Impact

- New public API: `FluidSize`, `TypeStyle.Fluid`, `TypeStyle.WithFluidSize`, `TypeStyle.AtWindow`;
  `TokenCss.FontSize`, `TokenCss.LineHeight` and `TokenCss.LetterSpacing` on the web. Nothing removed or renamed.
- The runtime gains the `FluidSize` twin and `TypeStyle.fluid`, `withFluidSize`, `atWindow`.
