- **2026-10-30 · Photon resolves a flex line as CSS does**: four Bugs under #166
  ([#693](https://github.com/eQuantic/equantic-ui/issues/693), [#728](https://github.com/eQuantic/equantic-ui/issues/728),
  [#744](https://github.com/eQuantic/equantic-ui/issues/744), [#747](https://github.com/eQuantic/equantic-ui/issues/747)).
  Photon's single-line pass shared a whole row by weight and never started a weighted Flexible from its basis
  (720 | 720 where Chrome gives 990 | 450), its wrapping pass broke a weighted Flexible without a basis at its
  child's natural size (two lines where Chrome shares one), a column took nothing back from an overflow (a zero
  weight 240 past its column's end), and a row that stretches asked a hand-kept list whether a cross size was
  explicit, which stretched a fixed Stack, Canvas, CameraPreview or ScrollView. Every item now starts from its flex
  base size and only the leftover is shared, weights under one sharing only that fraction, which also makes a
  weight animated to zero glide onto its basis instead of snapping there; a column gives its Flexibles' height
  back, and what fills it shares the overflow, so two Fill siblings in a column are 150 | 150 where `SizeKind.Fill`
  documented the second past the end; and one classifier, `DeclaredSizeKind`, answers both axes, asked of what a
  child measured to, through a component to what it built. Measured on the web realizer's own HTML in Chrome 152 and proved both ways on
  Photon. Proposed and archived through OpenSpec (`flex-layout`).
