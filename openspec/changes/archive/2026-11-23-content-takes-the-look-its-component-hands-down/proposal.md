## Why

Closes #1577, a slice of the feature #1545 (every ready-made component takes the developer's own content, builders
and style), under the epic #898. The design of #1546 decided that a component's content is a node and that a
`string` converts to one. Before any family slice (#1547 to #1551) can take its content as a node, that node has to
look as the component's own text looks today.

A `Button` draws its label in the button's type (13 to 17 over 600, by size), in its variant's colour per state, on
one line. Handed `Text("Save")` instead, the words would draw in `BodyL` and wrap, because a `Text` that names no
role is `BodyL` wherever it stands. The colour already follows the box around it (`BoxStyle.Foreground`, #614), which
is half of Flutter's `DefaultTextStyle` and `IconTheme`. The type, the line limit, the alignment and an icon's size
do not.

## What Changes

- **`BoxStyle.Content`**, a `ContentStyle`: the role, the type style, the line limit and the alignment a `Text` inside
  takes when it names none, and the size an `Icon` takes when it names none. A box nested in another lays its own
  over the outer one member by member (`ContentStyle.Merge`), as Flutter's `DefaultTextStyle.merge` does.
- **`Text` leaves what it does not name to the box above it**: `role`, `maxLines` and `align` become optional
  (`TypeRole?`, `int?`, `TextAlignment?`). A text that names none takes the nearest box's, and `BodyL`, no limit and
  the start where no box states one, so every tree written today draws as it did. A text that names its role keeps
  its own type whole: a box's type style applies only to a text that took the box's role.
- **`Icon`'s `size` becomes optional** (`float?`): the nearest box's `IconSize`, and 24 where none is stated.
- **A `string` is a node**: `VisualNode`'s implicit conversion makes a `Text` that names nothing, so a string passed
  where a node is taken draws in the look the box around it states. eqc carries it to the runtime twin's
  `VisualNode.fromString`, as it carries every vocabulary conversion.
- **Every realizer reads it the same way**, walking the tree with the look in effect: Photon's measure and paint, the
  web's lowering on the server and in the browser (where a component lowered again alone keeps the look it was
  lowered under), and the email realizer.

Breaks, preview: `Text.Role` is `TypeRole?`, `Text.MaxLines` is `int?`, `Text.Align` is `TextAlignment?` and
`Icon.Size` is `float?`. A reader of those members reads the resolved value through `Text.Under(ContentStyle?)` and
`Icon.SizeUnder(ContentStyle?)`; a writer changes nothing.

## Capabilities

### New Capabilities

- `content-defaults`: the look a box hands the text and icons inside it, and a string as a node.

### Modified Capabilities

None.

## Impact

- `eQuantic.UI.Primitives` (the contract): `ContentStyle`, `BoxStyle.Content`, `Text`, `Icon`, `VisualNode`'s
  conversion, `EffectiveTypeStyle`.
- The realizers: `eQuantic.UI.Native.Framework` (measure), `eQuantic.UI.Native.Components` (paint),
  `eQuantic.UI.Web` and the runtime's `lowering.ts` and `vocabulary.ts`, `eQuantic.UI.Email`.
- eqc: nothing new; the conversion crosses by the existing rule for a vocabulary type's operator.
- Principles followed: 4 (written once: one resolution, five producers), 6 (Flutter's `DefaultTextStyle` and
  `IconTheme`), 13 (the members widen and nothing keeps the old shape), 15 (this is what lets a component take its
  content as a node).
