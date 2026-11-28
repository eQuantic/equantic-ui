## Why

Closes #1916, a task of #1550 (dialogs, menus and notices take the developer's own content and a style of their own),
under the feature #1545. The family slices of #1545 turn what a component draws from the developer's content into
nodes, as the design of #1546 decided. Three of them hit the same wall:

- #1613 makes a `Dialog`'s title and message nodes. The dialog's layer is named by `Overlay.Label`, a string the
  `Dialog` fills with its title.
- #1609 makes a field's title, helper and error nodes. The entry is named by `TextEntry.Label` and described by
  `TextEntry.Description`, strings the field fills with them.
- #1607 makes a radio group's heading a node. The group is named by a string.

Once that content is a node, nothing in the vocabulary lets a part of the tree name or describe what it stands beside:
`Overlay`, `Group`, `LiveRegion` and `TextEntry` all take their name as a string. A dialog, a field or a group whose
title is a component of the app's own would be announced as "dialog", "text field" or "group" and nothing else.

Flutter answers the dialog with `Semantics(namesRoute: true)` around the title, and a field with `InputDecoration`,
whose label and helper are merged into the field's semantics node.

## What Changes

- **`LabelPart` and `DescriptionPart`**, two new vocabulary nodes: the part that names, and the part that describes,
  its nearest nameable. Each wraps any child, draws nothing, takes no geometry, takes no focus and is no stop of its
  own, so what it holds is read where it stands.
- **The nameables** are the containers that name themselves (a modal, open `Overlay`, an anchored panel whose role is
  a dialog, a `Group`, a `LiveRegion`) and the controls that take a name from beside them (a `TextEntry`, and a
  `Pressable` that is a checkbox, a radio or a switch).
- **A part reaches the nearest one.** Walking up from the part, it reaches the first ancestor that is a nameable, or
  that holds one nameable control that no nearer part reached and that carries no label of its own. So a field's label
  beside its entry names the entry, and a dialog's title names the dialog even when a field inside it is named by its
  own label. Every `Overlay` and every anchored panel is a scope, so a part inside a layer that is no dialog names
  nothing.
- **An ancestor holding two such controls is refused**, rather than guessed. The realizer fails with one sentence on
  every target, telling the developer to give each field its own `Column` or `Group`.
- **A part wins over the nameable's string**, as `aria-labelledby` wins over `aria-label` in the browser's name
  computation. The string stays the name of a nameable whose parts say nothing, which is every nameable written today.
  Several parts name or describe one nameable together, in reading order.
- **The web** writes the part as a host with an id, path-scoped like `MergedReading`'s, and the nameable's
  `aria-labelledby` or `aria-describedby` lists those ids, after any it already lists. The server's lowering and the
  browser's write the same markup. The name is the browser's to compute, so it follows the part's text when the text
  changes, and a component the browser lowers again alone keeps its parts' ids.
- **Photon** names and describes the nameable's stop by the text gathered under its parts. `SemanticNode` gains
  `Description`, which carries a `TextEntry.Description` too, the description the native track had fenced until now.
- **Email** passes through both, since a message has no nameable.

What a developer writes:

```csharp
Overlay(Column(children: [
    LabelPart(Row(children: [Icon(Icons.Warning), "Delete file?"])),
    DescriptionPart("It cannot be undone."),
    Row(children: [Button("Cancel"), Button("Delete")]),
]))

Column(children: [LabelPart("Email"), TextEntry(email, SetEmail), DescriptionPart("We never share it.")])
```

Nothing breaks. The public surface gains `LabelPart` and `DescriptionPart` in `eQuantic.UI.Primitives`, their
factories in `eQuantic.UI.Components`, `SemanticNode.Description`, and `Announcement.Description` in
`eQuantic.UI.Testing`. The developer surface outside C# does not move.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `screen-reader-parity`: a nameable is named and described by the parts that reach it, on every target.

## Impact

- `eQuantic.UI.Primitives` (the contract): `LabelPart`, `DescriptionPart`, their visitor methods and child slots, and
  `SemanticNode.Description`.
- `eQuantic.UI.Components`: the two factories.
- The realizers: `eQuantic.UI.Web` (`WebLoweringVisitor`, `NamingParts`), the runtime's `lowering.ts`,
  `naming-parts.ts`, `vocabulary.ts` and `nodes.ts`, `eQuantic.UI.Native.Framework` (measure),
  `eQuantic.UI.Native.Components` (emit, semantics and `NamedByParts`), `eQuantic.UI.Email`.
- `eQuantic.UI.Testing`: `Announcement.Description`, read from `aria-describedby` on the web.
- eqc: nothing new. The nodes cross as every vocabulary node does, to their runtime twins.
- Principles followed: 4 (written once: one rule, three producers held to it by the parity guards), 6 (Flutter's
  `namesRoute` and `InputDecoration`), 15 (this is what lets a component take its title, its label or its helper as a
  node without losing its name).
