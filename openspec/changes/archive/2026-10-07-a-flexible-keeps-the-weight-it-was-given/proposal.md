# Proposal

Closes #680, a Bug under #166 (Geometry and semantics live in the vocabulary). The wiki half of
#684, a Bug under #682 (An Image cannot fill its column), rides in the same pull request.

## Why

Cura reported it while putting photos on its public page (0.2.0-preview.60):
`Flexible(child, flex: 0, basis: 540)` rendered as `flex: 1 1 540px`, because the constructor
clamped the weight with `Math.Max(1, flex)`. "Start at 540 and never grow" became an item that grew:
at a window of 1440 the picture went from 540 to 657 px and squeezed the text beside it from 614 to
497. Measured again here, the clamp was not the only defect:

- The browser's twin never clamped, so the server wrote `flex: 1 1 540px` and the twin wrote
  `flex: 0 1 540px` for the same node. The two realizers disagreed on what the page hydrates.
- An object initializer walked past the clamp (`new Flexible(child) { Flex = 0 }`), and Photon's
  single-line pass then dropped the child altogether: it deferred every Flexible to the share pass,
  and that pass skips a weight of zero.
- A negative weight, basis or shrink was clamped as silently, where CSS drops the declaration.

## What Changes

- **A zero weight takes no share, on every target.** It is Flutter's `flex: 0` ("inflexible and
  determines its own size") and CSS's `flex-grow: 0`: the child keeps its basis, or its own size
  when it has none.
  - The web, the server render and the twin alike, writes `flex: 0 <shrink> <basis>`, with `auto`
    as the basis when there is none. `0%` would size an item that never grows at nothing, which is
    what a browser does with it (measured: a 200px child in a `flex: 0 1 0%` item is 0 wide).
  - Photon lays a zero weight out as a rigid item: in a slot of its basis, or measured from its
    content, where a Fill has nothing to fill, as in a browser. It gives space back by its own
    shrink on an overflowing line, and it does not make a hugging row take the available extent.
- **A negative weight, basis or shrink is refused where it is written**, and so is a basis that is
  not a finite number: an `ArgumentOutOfRangeException` from the C# init accessor, so the
  constructor and an object initializer both meet it, and a `RangeError` from the twin once its
  trailing config, the C# initializer, is applied.

For a developer: `Flexible(child, flex: 0, basis: 540)` renders at 540 and never grows, on the web
and on Photon. A tree that wrote a negative number now fails where it builds instead of rendering
a number nobody wrote.

## Capabilities

### New Capabilities

- `flex-layout`: how a flex container shares its main axis among its children, starting with the
  numbers a `Flexible` declares.

### Modified Capabilities

None.

## Impact

- Primitives: `Nodes/Flexible.cs`, where the three numbers are checked on their accessors.
- The web realizer: `WebLoweringVisitor.Containers.cs` (`LowerFlexible`).
- The runtime: the `Flexible` twin and `lowerFlexible`, in `vocabulary.ts` and `lowering.ts`.
- Photon: the single-line and wrapped flex passes (`MeasureVisitor.Flex.cs`) and `Shrinkable`.
- Not reached: eqc, the transpiled components (no component passes a zero or a negative weight), the
  shells, the SDKs and the templates.
- The public surface does not move: the constructor and the three `init` properties keep their
  signatures. The developer surface does not move. The break is in behaviour: a negative number
  throws, and a zero weight no longer grows.
- The wiki: WriteOnceComponents states the zero weight (English and Portuguese). For #684,
  SupportedFeatures names the `eQuantic.UI.Images` package and what it ships, and the
  EmailRealizer example gives its logo the height `Image` requires.
