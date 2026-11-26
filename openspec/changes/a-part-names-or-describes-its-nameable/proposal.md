## Why

Closes #1916, a task of #1550 (dialogs, menus and notices take the developer's own content and a style of their own),
under the feature #1545. #1613 makes a `Dialog`'s title a node, as the design of #1546 decided for every part a
component draws from the developer's content. Today the dialog's layer is named by `Overlay.Label`, a string the
`Dialog` fills with its title. Once the title is a node, nothing in the vocabulary lets a part of the tree name the layer
around it: `Overlay`, `Group` and `LiveRegion` all take their name as a string. A dialog whose title is a component
(an icon beside a word, a localized title built by the app's own component) would be announced as "dialog" and
nothing else.

Flutter answers it with `Semantics(namesRoute: true)` around the title: the title's own semantics names the route, so
the name follows whatever the title draws.

## What Changes

- **`TitlePart`**, a new vocabulary node: the part of a layer that names it. It wraps any child, draws nothing, takes
  no geometry and is layout-transparent. A modal, open `Overlay` with a `TitlePart` inside is named by what the part
  says, and a developer writes it as Flutter's authors write `namesRoute`:
  `Column(children: [TitlePart(Text("Rename file")), Text("The name is shared with everyone.")])` inside the layer.
- **The part wins over `Overlay.Label`**, as `aria-labelledby` wins over `aria-label` in the browser's name
  computation, so both targets agree when a layer has both. `Label` stays the name of a layer with no part.
- **Several parts name the layer together**, in reading order.
- **A part names the nearest layer only.** A layer opened inside it is named by its own parts, and a part outside every
  layer names nothing.
- **The web** writes the part as a host with an id, path-scoped like `MergedReading`'s, and the layer's
  `aria-labelledby` lists those ids, on the server's lowering and the browser's alike. The name is the browser's to
  compute, so it follows the part's text when the text changes.
- **Photon** names the layer's stop by the text gathered under its parts, the derived name it already gives a control
  with no `Label`.
- **Email** passes through it, since a message has no layer.

Nothing breaks. The public surface gains `TitlePart` in `eQuantic.UI.Primitives` and its factory in
`eQuantic.UI.Components`. The developer surface outside C# does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `screen-reader-parity`: a layer is named by the part that titles it, on every target.

## Impact

- `eQuantic.UI.Primitives` (the contract): `TitlePart` and its visitor method.
- `eQuantic.UI.Components`: the `TitlePart` factory.
- The realizers: `eQuantic.UI.Web` (`WebLoweringVisitor`), the runtime's `lowering.ts`, `vocabulary.ts` and `nodes.ts`,
  `eQuantic.UI.Native.Framework` (measure), `eQuantic.UI.Native.Components` (emit and semantics), `eQuantic.UI.Email`.
- eqc: nothing new; the node crosses as every vocabulary node does, to its runtime twin.
- Principles followed: 4 (written once: one node, every producer reads it), 6 (Flutter's `namesRoute`), 15 (this is
  what lets a dialog take its title as a node without losing its name).
