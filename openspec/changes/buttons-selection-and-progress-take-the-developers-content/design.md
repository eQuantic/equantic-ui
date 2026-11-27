## Context

#1580 decided how every family of the catalog takes the developer's content, builders and style, and left the
families to slices. #1606 gives this family its style and the radio group's option builder. This slice is the
content: a button, a submit, a chip, a checkbox and a radio group still take what they show as a string. The
mechanisms it needs are #1577's content look (`BoxStyle.Content`, a string as a node), on main, and #1916's part
that names the nearest nameable, for the group's heading.

## How Flutter solves it

`ElevatedButton` and its siblings take `child`, and `ElevatedButton.icon` takes `icon` and `label` widgets. The
button draws its child under a `DefaultTextStyle` and an `IconTheme` built from its `ButtonStyle`, so a bare `Text`
looks like the button's own label. `Chip` takes `label` and `avatar` widgets, under `labelStyle` and the chip's
icon theme, and `FilterChip` draws its checkmark over the avatar when selected. `CheckboxListTile` takes `title`.
In every one of them the semantics node of the control merges the text below it, so the words name the control.
This slice takes the widgets, the look (#1577 is our `DefaultTextStyle` and `IconTheme`) and the merged name. It
differs only by the names decision 1 gives them, and by one parameter per part, since a string converts to a node
and no string twin is needed.

## Decisions

### 1. The names follow decision 1

`Label` is the accessible name in this vocabulary, so content never takes it:
- a button's, a submit's and a chip's label becomes `child`, their single content;
- a checkbox's label becomes `title`, the text beside the box (`CheckboxListTile.title`);
- a radio group's label becomes `title`, its heading, the name subsection 3 gives a field's label (#1609).

The members follow the parameters: `Button.Child`, `FormSubmit.Child`, `Chip.Child`, `Checkbox.Title` and
`RadioGroup.Title`, each a `VisualNode`, the last two nullable as their labels were.

### 2. Each component states its look on the box around the content

Where the component drew `new Text(Label, TypeRole.Label, textColor, maxLines: 1) { StyleOverride = … }`, it now
wraps the developer's node in a box whose style states the same look, `Content = new ContentStyle { Role, TypeStyle,
MaxLines }` and `Foreground`. A bare `Text` or a `string` is then drawn exactly as before, and a node that names its
own look keeps it. The pins with a plain string are the proof: the output does not move. The button's and the
chip's box also state the `IconSize` they drew their own icon at, so an `Icon` in the child is sized as the leading
icon is.

### 3. A control is named by its content, and a group by its heading's part

A button's, a submit's, a chip's and a checkbox's content is inside the `Pressable` the component returns. The
component stops handing the `Pressable` its words as `Label`, and both targets already name a control that has none:
- the web's `<button>` is named by the text inside it;
- Photon's semantics gathers every `Text` under the control (`TextWithin`).

So a child that is a component of the developer's own still names the button, with nothing gathered at the
component's build, where a component's words do not exist yet.

The radio group's heading is beside its options, not around them, and the group's name is the `Adjustable`'s
`Label` today. The heading is marked with #1916's naming part, which names the group it sits in, so the group is
named by the heading's words on both targets.

Rejected: a helper that reads the words out of the child at the component's build (`VisualNode.TextOf`). It misses a
component handed as the child, which is the case the change exists for (#1609 rejected it for the same reason).

### 4. A chip's avatar takes the check's place

`Chip(avatar:)` is drawn before the child at the chip's icon size and in its text colour, as Flutter's `Chip.avatar`
is. A selected filter chip draws its check in the avatar's place, as `FilterChip` does, so the chip keeps its width
when it toggles. With no avatar, the check is drawn as today.

### 5. What stays as it is

- `Button.Leading` stays an `IconGlyph`: the button swaps it for the spinner while loading, and sizes it from the
  size table. A developer who wants a picture of their own puts it in the child, where #1577's `IconSize` sizes an
  `Icon` the same way.
- The entries the family keeps as text stay with their reasons, `IconButton.Label`, `Switch.Label`, `Slider.Label`
  and `ProgressBar.Label` among them: a control whose content carries no text takes a string for its name
  (decision 4 of #1580).

## Risks / Trade-offs

- **A child with no text** (an icon alone) leaves the button unnamed, as an `IconButton` with no label would be. The
  developer reaches for `IconButton`, which takes the name, and Photon's semantics guard reports a nameless control
  where it can.
- **A checkbox with no title** has no name, as one with no label has today. Nothing regresses, and the case stays
  `Checkbox`'s to answer in its own issue if a table's row needs it.
- **#1606 edits the same constructors**: a merge of `main` resolves it, keeping the node first and `style:` last.
- **Every call site that named a renamed parameter or read a renamed member moves at once**, in the samples, the
  tests and the wiki's five languages, in this pull request: a preview break, with one migration line per name.
