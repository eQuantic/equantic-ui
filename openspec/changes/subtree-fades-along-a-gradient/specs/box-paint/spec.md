## ADDED Requirements

### Requirement: A subtree fades along a mask from a closed set that every realizer draws

`OpacityMask(OpacityGradient gradient, VisualNode child)` SHALL composite its child as one group and multiply it, at every point, by the
mask's opacity there. `OpacityGradient`'s kinds SHALL be `LinearOpacityGradient` and `RadialOpacityGradient` and no other: a type outside the
vocabulary's assembly SHALL NOT be able to derive from `OpacityGradient`, and every realizer SHALL draw a mask through
`IOpacityGradientVisitor<TState, TResult>`, so adding a kind is a build error in every realizer that does not draw it, and a type
error in the runtime's twin.

The gradient's box SHALL be the node's bounds. A `LinearOpacityGradient`'s angle SHALL be in degrees clockwise from "toward the
top", its gradient line through the box's centre with length |W·sin θ| + |H·cos θ|. A `RadialOpacityGradient`'s centre SHALL be
fractions of the box, and its ellipse SHALL be sized by its `RadialExtent`: fractions of the box per axis, dp, or the
closest side, the farthest side, the closest corner or the farthest corner, as CSS Images 3 sizes an ellipse.

A mask SHALL take any number of stops, one or more, and resolve them as CSS Images 4 resolves color stops: a stop whose
offset is below an earlier stop's SHALL move up to the largest earlier offset, repeated offsets SHALL make a hard step,
offsets outside 0..1 SHALL be kept, and a single stop SHALL be that opacity everywhere. The opacity SHALL be piecewise
linear between the resolved stops, the first stop's before it and the last stop's after it. Both targets SHALL draw
the resolved list, and the web SHALL write it rather than the stops as given.

#### Scenario: Cura's hero field

- **WHEN** a white 400 × 300 box over black is wrapped in `OpacityMask(new RadialOpacityGradient(new(0.7f, 0.35f),
  RadialExtent.Fraction(0.7f, 0.7f), [new(0.35f, 1), new(0.76f, 0)]), box)`
- **THEN** at every pixel centre the opacity is within 1/255 of the analytic value on Photon's Reference backend, and
  within 0.5/255 in Chrome 157 and Firefox 159, where the web writes `radial-gradient(70% 70% at 70% 35%,
  rgb(0 0 0 / 1) 35%, rgb(0 0 0 / 0) 76%)`

#### Scenario: Closest side on a wide box

- **WHEN** a 400 × 240 box is masked by `RadialOpacityGradient(new(0.5f, 0.5f), RadialExtent.ClosestSide, [new(0, 1), new(1, 0)])`
- **THEN** the ellipse's radii are 200 and 120 on both targets, and the web writes
  `radial-gradient(closest-side at 50% 50%, …)`

#### Scenario: A linear mask at 135 degrees

- **WHEN** a 400 × 240 box is masked by `LinearOpacityGradient(135, [new(0, 1), new(1, 0)])`
- **THEN** the gradient line is 452.548 dp long, the top-left corner is opaque and the bottom-right transparent, on
  both targets, and the web writes `linear-gradient(135deg, rgb(0 0 0 / 1) 0%, rgb(0 0 0 / 0) 100%)`

#### Scenario: A hard stop

- **WHEN** a mask's stops are `[new(0.5f, 1), new(0.5f, 0)]`
- **THEN** each pixel centre is fully opaque or fully transparent on both targets, as the browsers draw it

#### Scenario: Stops resolved as CSS resolves them

- **WHEN** a linear mask's stops are `[new(0.6f, 1), new(0.3f, 0)]`, or `[new(-0.5f, 1), new(1.5f, 0)]`, or
  `[new(0.3f, 0.5f)]`
- **THEN** the first is opaque up to 60% and transparent from it, the second is 0.75 at the start and 0.25 at the end,
  and the third is 0.5 everywhere, on both targets, as Chrome 157 and Firefox 159 draw the same CSS within 0.95/255

#### Scenario: Eight stops

- **WHEN** a mask has eight stops of alternating opacities
- **THEN** both targets draw the piecewise-linear opacity within 1/255 of the analytic value at every pixel centre

#### Scenario: Refusals

- **WHEN** a mask is given no stop, an opacity of 1.2, a non-finite offset, a non-finite centre, or a `Fraction`
  extent of 0
- **THEN** its constructor throws `ArgumentOutOfRangeException` naming the parameter (`stops`, `center`, `x` or `y`)

### Requirement: A mask changes neither layout nor where the subtree takes the pointer

A `OpacityMask` node SHALL be layout-transparent: its parent SHALL size, floor and shrink it as it would its child. A mask
SHALL NOT change any hit test: a point where the mask's opacity is zero SHALL take the press, the hover and the pointer
it took without the mask, on both targets. What the child draws outside the node's bounds SHALL be cut, on both
targets.

#### Scenario: A press where the mask hides everything

- **WHEN** a `Pressable` sits where a mask's opacity is zero and the point is pressed
- **THEN** on Photon, driven through `PhotonHost` across a frame, the press reaches the `Pressable`; and in Chrome and
  Firefox `elementFromPoint` at that point returns the pressable's element

#### Scenario: A child that overflows

- **WHEN** a masked box's child is positioned 40 dp outside it, or the box casts a spread shadow
- **THEN** nothing of either is drawn outside the masked node's bounds, on both targets

#### Scenario: A fill child

- **WHEN** an `OpacityMask` holds a box whose width is `Fill` inside a row
- **THEN** the wrapper element fills the row on the web, as the box would alone, and Photon lays out the same width

### Requirement: Photon composites a masked subtree once, crisp at any scale and under any transform

Photon SHALL render a masked subtree into one layer and composite it once, multiplying each texel by the mask's
opacity evaluated at the device pixel in the node's local space, so overlapping children never double-blend, the mask
follows the node's transform and is exact at any scale. The stops SHALL be read texel by texel, unfiltered, from a
stop texture of 32-bit floats, cached per host by the resolved stops' value, so a mask that does not change uploads
nothing after its first frame. The Reference backend SHALL be normative, and Metal and Vulkan SHALL match it within the
GPU parity tolerance on the offscreen path. A live window SHALL draw the same once its present path renders layers
offscreen (#1952).

#### Scenario: Under a rotation and a scale

- **WHEN** the hero's masked box is drawn under a 30° rotation at 2.625 device pixels a dp
- **THEN** the opacity at every pixel centre is within 1/255 of the mask's value at that pixel's local point

#### Scenario: A hard stop on the GPU

- **WHEN** the `opacity-mask-layer` golden's hard-stop mask is drawn on Metal and on Vulkan
- **THEN** each pixel centre is fully opaque or fully transparent, as on the Reference backend

#### Scenario: The stop texture is cached

- **WHEN** two masks with equal resolved stops are drawn for a hundred frames
- **THEN** one stop texture is created and uploaded once, and a mask whose stops differ in one float gets its own

#### Scenario: Nested with a group opacity

- **WHEN** a masked box sits inside a box of opacity 0.5, and another masked box holds one
- **THEN** each pixel is the product of both alphas, within 1/255, and overlapping children inside either do not
  double-blend

### Requirement: Cura's azulejo is held by one golden both targets are compared with

The azulejo SHALL be written in C# with the vocabulary alone: a `DrawingPattern` over a 58 × 58 `VectorDrawing` of two
2 dp bands, a centre circle of radius 5.1265 and four corner circles of radius 26.658, under an `OpacityMask`. One image per
scene, written by Photon's Reference backend, SHALL be the golden: Photon SHALL match it within 2/255 per channel,
and the web, served and drawn by Chrome in CI, SHALL match it within the tolerances the design records. Cura's dark
slabs SHALL be held by each target's own golden and listed as fenced from the comparison, naming #1953, until the two
targets blend translucency alike.

#### Scenario: The fade's probe

- **WHEN** a white box under each mask kind is drawn on a transparent ground at a device scale of 2 by Photon and by
  Chrome from the served sample
- **THEN** both alpha channels are within 2/255 of the golden at every pixel

#### Scenario: The hero's field

- **WHEN** the hero's field is drawn over the page's cream by both targets
- **THEN** both match the golden within 4/255 per channel at 99% of pixels and within the recorded edge bound at
  any, and a mask written at 60% instead of 70% on either target fails the comparison

#### Scenario: The dark slabs

- **WHEN** the cross-target comparison runs
- **THEN** it compares no slab, and lists the three as fenced, pointing at #1953
