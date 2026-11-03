## Why

Closes #674, a sub-issue of Feature #194.

Cura's family waitlist picks a concelho on a map of the Porto district, drawn as a `Drawing` of `VectorShape` paths. A `Drawing` was static artwork: no shape could be pressed, focused, hovered or named, so the app picked the concelho from a list and repainted the map. Laying `Pressable`s over the shapes is fragile, because `Positioned` offsets are fixed while the drawing scales with its box, and a box is the wrong target for a region whose neighbours reach into it.

## What Changes

- `VectorShape` gains `Label`, `OnPressed`, `Selected` (`bool?`) and `Hover` (`StyleDiff`), the types `Pressable` uses for the same things. A shape with `OnPressed` is an input:
  - it takes a press where its path is painted, by its own fill rule, never across its bounding box;
  - it is a stop of the keyboard's focus order, and Enter and Space press it;
  - it is announced as a button named by `Label`, with `Selected` as its state;
  - `Hover` is laid over its paint while the pointer is over it, and the keyboard's focus strokes its outline in the theme's focus colour, 2dp whatever the drawing is scaled to.
- `Hover` reaches a path through the members a path has: `Background` is its fill, `BorderColor` and `BorderWidth` its stroke, and `Opacity` its opacity. A diff that sets any other member (elevation, gradient, backdrop blur, transform, shadows) is refused when the shape is built, on both targets, with the same message.
- A drawing with input shapes is a group and is never hidden from assistive tech, labelled or not.
- On the web each input shape is a `path` with `role="button"`, `tabindex="0"`, `aria-label`, `aria-pressed`, `pointer-events: visibleFill`, a click and a key handler, and its hover as an atomic `:hover` rule. The theme's stylesheet gains the `.eq-shape` focus rule. Server rendering and the client lowering write the same tree, pinned by the component parity fixture.
- On Photon each input shape registers a hit region tested against its path (`ShapeHit`, the winding number of the point by the shape's fill rule), under an id its drawing's position keeps for it (`NodeElement.PartId`), so the press, the focus, an activation and the semantics node name the same shape. The host follows the shape under the pointer (`HoveredPart`) and the drawing paints its hover and its ring.
- The dashboard sample gains a `/map` page.

## Impact

- Reaches the vocabulary (`eQuantic.UI.Primitives`: `VectorShape`), the runtime's twin and client lowering (`vocabulary.ts`, `nodes.ts`, `lowering.ts`), the web realizer (`WebLoweringVisitor.Graphics`, `ThemeStylesheet`) and Photon (`EmitVisitor.Media`, `SemanticsVisitor.Graphics`, `PhotonHost`, `PhotonRealizer`, `InputSink`, `PressScope`, the new `ShapeHit`).
- The public surface grows: `VectorShape.Label`, `OnPressed`, `Selected`, `Hover` and `IsInput` (contract), and `HitRegion.Id`, `PhotonHost.HoveredPart`, an `id` on `PhotonHost.Focus` and a `hoveredPart` on `PhotonRealizer.Realize` (experimental). `developer-surface.baseline.txt` does not move.
- No break for an app: a shape without `OnPressed` draws as it did. The experimental `PhotonHost.Focus` and `PhotonRealizer.Realize` widen by an optional parameter.
