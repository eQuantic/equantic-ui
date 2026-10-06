# Design

## Context

The hit contract (spec §08) is kept in three places: `Sizing.HitTarget` answers a control's target
for its rung, Photon's `EmitVisitor.ExpandHitRect` grows every interactive node's hit rect, and the
web's token sheet grows every `.eq-pressable` with a pseudo-element under `@media (pointer: coarse)`.
Under a pointer all three answered the visual bounds, so a 20dp selection box was a 20dp target.

## How Flutter solves it

Material's `MaterialTapTargetSize` pads a control to 48×48 on a phone and shrink-wraps it on desktop,
where `VisualDensity` also tightens it, so Flutter on desktop keeps no floor at all: a dense
checkbox's target is its own bounds. The SDK already makes the same split from the target
(`Density` follows the input), and it does better by keeping WCAG 2.2's 24dp under a pointer, which
Flutter leaves to the author.

How the slop is hit is the part Flutter gets right and the web did not. `_InputPadding`, the render
object that pads a tap target, grows the area it answers for and redirects the hit to the child, and
`RenderBox.hitTest` asks the children before the parent anyway: the padding answers only where the
child is not. `docs/FLUTTER-PARITY.md` §5 lists `HitTestBehavior` as realizer-side here, and this is
that behaviour.

## Decisions

**The floor is a token.** `Touch.MinPointerTarget` sits beside `Touch.MinTarget`, and the three places
read it: `HitTarget` floors its Compact answer at it, `ExpandHitRect` takes it as the Compact minimum,
and the web's fine-pointer gate grows the slop to it. The handoff publishes it at
`touch.minPointerTarget`, so `HandoffTokenPinTests` compares it and `HandoffSdkCoverageTests` counts
it.

**The web's slop lies under the content.** The slop was `::after` with `position: absolute`, so it
came last in tree order and painted, and was hit, over the control's content. It is now `::before`,
and the pressable's children are lifted with `:where(.eq-pressable) > * { position: relative; }`:
positioned boxes with no z-index paint in tree order, so the content comes after the slop and is hit
first, and the slop answers only around the control. The lift has no specificity, so a child that
positions itself (a raised box carries `position: relative` and its elevation as a z-index, a layer
of a Stack its own) keeps its own position.

Rejected:

- **Making the pressable a stacking context** (`z-index: 0`, the slop at `z-index: -1`). It puts the
  slop under the content too, but it traps every z-index inside the pressable. A raised box carries
  its elevation as a z-index, so that it stays above what is drawn after it, and inside a stacking
  context that z-index orders it only among the pressable's own children: a raised card inside a
  Pressable would go under every raised box around it.
- **Growing only the controls known to be small.** The realizer knows a Checkbox's 20dp box, but not
  an app's own Pressable around a 16dp icon, and two lowerings (the C# realizer and the runtime's
  twin) would have to agree on the decision for hydration.
- **Forwarding the hover in the atomizer** (a rule for `.eq-pressable:hover > .eq-x` beside
  `.eq-x:hover`). It mends the hover alone, changes every hover rule in two twins, and leaves a
  control inside a Pressable without its own hits.

## Risks

- An absolutely positioned descendant of a pressable's content is now anchored to the content
  instead of the pressable. The two are the same box: the pressable is a button with no padding
  around one child.
- The content is positioned, so it paints after the in-flow boxes around it, and in tree order
  among positioned boxes with no z-index. Everything the SDK layers (pinned and floating chrome,
  overlays, a Stack's layers, raised boxes) carries an explicit z-index, so nothing the SDK draws
  changes order.
- On a touch screen a control inside a Pressable now takes its own tap, and the press bubbles to the
  wrapper. That is what a mouse already did, so the two pointers now agree.
