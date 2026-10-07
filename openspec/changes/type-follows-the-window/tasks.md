# Tasks

## 1. The size

- [x] 1.1 `FluidSize` and `TypeStyle.Fluid`, `WithFluidSize` (refusing what cannot be) and `AtWindow`; `WithSize` drops the fluid size
- [x] 1.2 The runtime twins: `FluidSize`, `TypeStyle.fluid`, `withFluidSize`, `atWindow`, exported

## 2. The targets

- [x] 2.1 The web realizer (C#) and the TypeScript twin write the clamp and a unitless line height for a style override, byte-identical; the role class does too
- [x] 2.2 Photon measures and paints at the window the frame lays out against
- [x] 2.3 The tracking follows the size too: `em` on the web, scaled by `AtWindow` on Photon
- [x] 2.4 Pinned: `FluidTypeRealizerTests` and `fluid-type.spec.ts` (web), `FluidTypeTests` (Photon)

## 3. The record

- [x] 3.1 The wiki, English and Portuguese, on this pull request's wiki branch: DesignSystem (the type scale)
- [x] 3.2 `docs/LEDGER.md` one line citing #652; the wiki snippet compiled in `WikiClaimsCompile`
- [x] 3.3 PublicAPI updated; the full suites, each alone and read by its exit code: Web (with the wiki guards), Native.Engine, Design, Compiler, Conformance, Server, Email, Images, and the runtime's 2,132 specs
- [x] 3.4 Proven on falei.pt's headings in a browser: one `h1` where there were three, 53.76px at 1280 (4.2vw) and 34px at 375 (the floor), the tracking at -0.035em
