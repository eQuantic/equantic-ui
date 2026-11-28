Each section is one sub-issue of #1938 on the board, and the pull request closes them together, as #1937's did. #1952
blocks #1938: task 5.6 waits on it. The measurements behind every number below are in
`.tools/drafts/fade-1938/measure/`, outside the repository.

## 1. The vocabulary: a mask of any number of opacity stops

- [x] 1.1 Measure, in Chrome 157 and Firefox 159 at device pixel ratios 1 and 2, `mask-image` against the analytic
  opacity of design.md decision 4: the hero's ellipse, `closest-side` on three boxes, an off-centre `farthest-corner`,
  a 135° and a 200° linear mask, a hard stop. Within 0.5/255, 1.0 for Firefox's three-stop linear, exact at the hard
  stop.
- [x] 1.2 Measure CSS's stop rules in the same engines: no stop (refused), one stop (a solid opacity), out-of-order
  offsets (moved up), repeated offsets (a step), offsets outside 0..1 (kept), eight stops. Within 0.95/255 of the rules
  of decision 4.
- [x] 1.3 `OpacityGradient`: an abstract record, `private protected` constructor, `Stops : IReadOnlyList<OpacityStop>` as written,
  `Resolved : IReadOnlyList<OpacityStop>` after the fix-up, `float OpacityAt(float t)` over `Resolved`, and
  `abstract TResult Accept<TState, TResult>(IOpacityGradientVisitor<TState, TResult> visitor, TState state)`.
  `IOpacityGradientVisitor<in TState, out TResult>` has `Visit(LinearOpacityGradient, …)` and `Visit(RadialOpacityGradient, …)`. One file each.
- [x] 1.4 `LinearOpacityGradient(float angle, IReadOnlyList<OpacityStop> stops)` with `Axis(Size box)`, and `RadialOpacityGradient(Point center,
  RadialExtent extent, IReadOnlyList<OpacityStop> stops)` with `Radii(Size box)`, sealed records, the formulas of decision
  4. `OpacityStop(float Offset, float Opacity)`, a readonly record struct.
- [x] 1.5 `RadialExtent`: a readonly record struct of `RadialExtentKind` and two values, with `Fraction(x, y)`,
  `Dp(x, y)`, `ClosestSide`, `FarthestSide`, `ClosestCorner` and `FarthestCorner` (the default). `RadialExtentKind`
  (`FarthestCorner = 0, ClosestSide = 1, FarthestSide = 2, ClosestCorner = 3, Fraction = 4, Dp = 5`) joins
  `EnumValueAbiTests`.
- [x] 1.6 The refusals, each an `ArgumentOutOfRangeException` naming its parameter: no stop, an offset that is not
  finite, or an opacity outside 0..1 (`stops`); a centre that is not finite (`center`); a `Fraction` or `Dp` value that
  is not positive (`x`, `y`). Nothing else is refused: an out-of-order, a repeated or an out-of-range offset is CSS's,
  and resolved as CSS resolves it.
- [x] 1.7 `OpacityMask(OpacityGradient gradient, VisualNode child) : SingleChildNode`, `NodeKind => "opacityMask"`, layout-transparent by
  `LayoutTransparency`'s default. `IVisualNodeVisitor` gains `Visit(OpacityMask, …)`. The factory `OpacityMask(OpacityGradient gradient,
  VisualNode child)` in `eQuantic.UI.Components`' `UI`.
- [x] 1.8 `./scripts/public-api.sh update`, and read the diff: additions only, in Primitives and Components.
- [x] 1.9 `OpacityGradientVocabularyTests` (new), each expected number the browsers' from 1.1 and 1.2: every refusal; `Axis` at
  0°, 90°, 135° and 200° on a 400 × 240 box against L = |W sin θ| + |H cos θ|; `Radii` for each extent on a centred and
  an off-centre point, and the degenerate `ClosestSide` on an edge; `Resolved` and `OpacityAt` for every row of
  decision 4's stop table; the value equality of each kind. Each case fails with the fix-up removed, or with it
  applied as "sort the stops" (the plausible wrong one).
- [x] 1.10 Check: the vocabulary builds, and the node visitors (Web, Email, Photon's emit, semantics and measure, and the
  twin's `tsc`) fail until they have `Visit(OpacityMask, …)`, which is the instrument working.

## 2. The web and the runtime twin: `mask-image`, written alike by SSR and the twin

- [x] 2.1 Measure in Chrome and Firefox: a mask cuts what overflows its element, its box is a rectangle whatever the
  radius, an ancestor's rounded `overflow: hidden` clips it, and a point it hides still hit-tests.
- [ ] 2.2 `OpacityGradientCss : IOpacityGradientVisitor<…, string>` writes decision 5's table from `OpacityGradient.Resolved`, through `TokenCss`'s
  formatter, a single stop written at 0% and 100%.
- [ ] 2.3 `HtmlStyle.MaskImage` and its `mask-image` entry in the CSS writer.
- [ ] 2.4 `LowerOpacityMask`: one wrapper `div` carrying `mask-image`, the child inside, joining `Fills`, `CapsAt` and every
  list the layout spec's "Every layout-transparent wrapper passes its child's fill through" names. `PaintsNothing`
  and the `Stack`'s positioned-child resolution see through it.
- [ ] 2.5 The twin: `OpacityMask`, `OpacityGradient` (with `resolved`), `LinearOpacityGradient`, `RadialOpacityGradient`, `OpacityStop`, `RadialExtent` and
  `RadialExtentKind` in `vocabulary.ts`, with `gradientKind` and the exports (`runtime-exports.ts`, `index.ts`);
  `lowerOpacityMask` and `opacityGradientCss` in `lowering.ts`, ending in an exhaustive `never` check; the same wrapper lists.
- [ ] 2.6 `OpacityMaskRealizerTests` (new): each row of decision 5's table, the resolved and the single-stop rows included; a
  fill child makes the wrapper fill; a `Positioned(OpacityMask(Box))` in a `Stack` still spans. CROSS-PINNED by
  `opacity-mask.spec.ts` on the twin. `ComponentParityFixtureTests` and `component-parity.spec.ts` gain a masked node of each
  kind.
- [ ] 2.7 The email realizer lowers an `OpacityMask` as its child, and its fence list says why (a mail client has no mask).
- [ ] 2.8 Check, against the real thing: a page with every mask of 1.9 in Chrome, Firefox and iOS Safari, served and
  reached by a link, with no hydration warning, and `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` green.

## 3. Photon: a masked layer whose stops are read exactly from a data texture

- [ ] 3.1 `TextureFormat.R32Float = 2`, appended, and `TextureData.Floats(int width, int height, ReadOnlySpan<float>
  values)`. Metal's and Vulkan's `CreateTexture` choose the format and the bytes per row from a table keyed by
  `TextureFormat` (`MTLPixelFormatR32Float`, `VK_FORMAT_R32_SFLOAT`), replacing their `rgba` boolean.
- [ ] 3.2 `LayerOpacityMask` (Engine): the geometry, `ParameterAt(Point local)` (unclamped) and `OpacityAt` over a stop
  texture's floats, the normative math. `DisplayListBuilder.PushOpacityMaskLayer(Rect local, Paint geometry, TextureData
  stops)`: a `BeginLayer` carrying the mask in the slots of decision 6 and the stop texture in the frame's table, closed
  by `PopLayer`. A plain `PushLayer` is unchanged.
- [ ] 3.3 `OpacityStopCache` (Components), per host beside `PatternTileCache`, through `MotionScope`: the stop texture of
  a mask's `Resolved`, keyed by `StopsKey` (every float's bits, the hash computed once), behind a
  `ConditionalWeakTable<OpacityGradient, TextureData>`, least recently used out past 256 entries.
- [ ] 3.4 The Reference backend: the `EndLayer` of a masked scope composites through `LayerOpacityMask.OpacityAt`, 0 outside
  the local rect, times the alpha and the clip.
- [ ] 3.5 `opacity_mask_composite` in `Sdf.slang`, transliterating 3.4 with the uniform reuse of decision 6 and the
  stops read by `Load` from the coverage slot. `RhiPipelineKind.OpacityMaskComposite` appended; the Metal and Vulkan
  devices register it; `./scripts/generate-shaders.sh` regenerates `.metal`, `.spv` and `.metallib`, never edited by
  hand.
- [ ] 3.6 `RhiRenderer.EncodeRange` composites a masked layer, rendered by phase one, through the new pipeline.
- [ ] 3.7 `EmitVisitor.Visit(OpacityMask, …)`: `PushOpacityMaskLayer` with the node's bounds, the geometry of 1.4 at its laid-out
  size and the cache's stop texture, then the child, then `PopLayer`, settling the input targets past the layer as the
  opacity path does (EmitVisitor.cs:110-121). `SemanticsVisitor` and `MeasureVisitor` pass through.
- [ ] 3.8 `OpacityStopCache` tests: one texture for two masks with equal resolved stops, two for stops that differ in one
  float's last bit, none uploaded again across a hundred unchanged frames.
- [ ] 3.9 `OpacityMaskLayerTests` (Reference): a white box under each mask of 1.9, every stop case included, on a
  transparent ground, its alpha within 1/255 of `OpacityGradient.OpacityAt` at every pixel centre, and a hard stop exact; the
  same under a 30° rotation and at 2.625 device pixels a dp; inside a rounded clip; nested in a group opacity of 0.5
  and around one; a child that overflows the node cut at its bounds. Each fails with the mask removed, and with the
  mask evaluated in device space instead of local (the plausible wrong one). `OpacityMaskHitTestTests`: a `Pressable` under
  the mask's zero, driven through `PhotonHost` across a frame, takes the press.
- [ ] 3.10 Check: a golden `opacity-mask-layer` in `GoldenScenes`, every stop case on one surface, held on Metal by
  `MetalParityTests` and on Vulkan by `VulkanParityTests` (on the Windows runner) within `GpuParity`'s tolerance.

## 4. The overhang: a decoration past its surface, pinned on both targets

- [x] 4.1 Measure in Chrome and Firefox: unclipped, a `Positioned` past its stack paints over both of the stack's
  neighbours and takes their pointer, with or without `isolation: isolate`; with the surface clipped, nothing reaches
  them. The neighbours' case is #1954, outside this change.
- [ ] 4.2 The web: `StackRealizerTests` and `stack.spec.ts` pin that a `Stack`'s first child, a `Positioned` at negative
  offsets and with fractions, lowers to an anchor below the later children's `z-index`, inside a surface whose `Clip`
  writes `overflow: hidden` and its radius, and that the hero's anchor of decision 10 writes the edges that give 92%
  and 96%.
- [ ] 4.3 Photon: `OverhangTests` through the Reference backend: the same tree paints the field behind the stack's later
  children and above the surface's background, reaches 64 dp above and 80 dp past the end, and is clipped to a `Clip`
  surface's rounded rectangle. Each fails with the field placed last, and with the surface's clip removed.

## 5. The azulejo in C#, held by one golden both targets are compared with

- [ ] 5.1 `samples/DefaultUIDashboard/Screens/Azulejo.cs`: the tile drawings and the four fields of decision 10 as
  static members, no page attribute. Linked into `samples/PhotonDesktop` and the native test project with
  `<Compile Include="…" Link="…" />`.
- [ ] 5.2 `AzulejoScreen` (`/azulejo`) in the dashboard, linked from `/desk` and named in `ConsoleLayout`, and the same
  screen in PhotonDesktop's studio: the hero's field behind a profile card, and the three slabs.
- [ ] 5.3 `AzulejoGoldenTests` (native): `fade-probe`, `azulejo-hero` and the three slabs through the Reference backend
  into `Goldens/*.png` at a scale of 2, held at ±2/255. The slabs' goldens are Photon's own, and their web half is the
  component pins of their CSS.
- [ ] 5.4 `scripts/check-cross-target-goldens.cs`, run by CI's `samples` job after the samples build: serves the
  dashboard, opens `/azulejo` in headless Chrome over the DevTools protocol at a device scale of 2, and compares
  `fade-probe` (alpha, 2/255) and `azulejo-hero` (4/255 at 99% of pixels, the edge bound measured on this first run
  and written, never above 24/255) with their PNGs; the slabs are listed as fenced, pointing at #1953. It fails naming
  the scene, the worst pixel and the path of an amplified diff. `tests/e2e/tests/azulejo.spec.ts` makes the same
  comparison in Firefox, locally.
- [ ] 5.5 Prove the instrument both ways: with the web's hero mask written at 60% instead of 70%, and with Photon's
  mask evaluated in device space, the script fails.
- [ ] 5.6 **Waiting on #1952.** Once #1952 has merged: PhotonDesktop's `/azulejo` screen in a window on Metal at two
  scales and on the Windows runner on Vulkan draws the hero's fade as the golden does (a screenshot of the window's
  drawable compared with `azulejo-hero` within 5.4's tolerance), and the masked layer is no longer drawn unmasked.
- [ ] 5.7 Check: CI's `samples` job green with the script, the two samples opened side by side with a picture of each in
  the pull request, and Cura's four fields compared with its page by eye.

## 6. Documentation and the ledger

- [ ] 6.1 The wiki, in its five languages (English at the root; Portuguese, Spanish, French and German under
  `locale/`), on a branch named like this change's: the styling page gains "Fading a subtree" (the two kinds, the
  stops as CSS resolves them, the extents, the CSS each writes, that a mask changes no hit test and cuts what
  overflows the node) and "A decoration past its surface" (a `Positioned` at negative offsets as the stack's first
  child, `Clip` on the surface, and that on the web an unclipped overhang paints over the stack's neighbours, #1954),
  with the azulejo as the example.
- [ ] 6.2 `docs/FLUTTER-PARITY.md`: a row for `ShaderMask` (SAME in shape, wider: an elliptical extent, CSS's sizes and
  CSS's stop rules; fenced: an image or a drawing as the mask, and every blend mode but `dstIn`).
- [ ] 6.3 `docs/NATIVE-GPU-ENGINE-PLAN.md`: a dated entry for the masked layer and the `R32Float` stop texture, naming
  #1952 for the live window.
- [ ] 6.4 `docs/ledger/<date>-subtree-fades-along-a-gradient.md`, one dated entry citing #1938 and #1042: the fade, the
  overhang pinned, the cross-target golden, and the measurements.
- [ ] 6.5 Check: the docs guards pass with `EQ_WIKI_DIR` pointed at a worktree of the wiki branch, and `LedgerTests` is
  green.
