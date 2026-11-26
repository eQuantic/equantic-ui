## Context

#1580 decided how every family of the catalog takes the developer's content, builders and style, and left the
families to slices. #1608 gave this family its style and builders. This slice is the content: the inputs and
pickers still take their label, helper, error, hint and invitation as strings. Two mechanisms it needs exist or
are being built: #1577's content look (`BoxStyle.Content`, a string as a node), on main, and #1916's part that
names or describes the nearest nameable, which subsection 2 builds for the dialogs (#1613) and widens to fields.

## How Flutter solves it

`InputDecoration` takes `label`, `helper`, `error`, `prefix` and `suffix` as widgets beside their `*Text` string
forms, and `DropdownButton` takes `hint` as a widget. The decorator draws each in the theme's style for that part
(`labelStyle`, `helperStyle`, `errorStyle`) through a `DefaultTextStyle`, so a bare `Text` looks like the
decorator's own text. The text field's semantics node takes its label from the decoration, merged under the
field. This slice takes the widgets, the per-part look (#1577 is our `DefaultTextStyle`) and the label naming the
field, with two differences decision 1 and 2 already settled: the names (`title`, not `label`) and one parameter
per part, since a string converts to a node and no `*Text` twin is needed.

## Decisions

### 1. The names follow decision 1

`Label` is the accessible name in this vocabulary, so content never takes it: a field's label becomes `title`, the
pickers' `dateLabel` and `timeLabel` become `dateTitle` and `timeTitle`, and `LabelHidden` becomes `TitleHidden`.
`helper`, `error` and `hint` are Flutter's names and carry no other meaning here, so they stay and take a node.
A select's `placeholder` becomes `hint`, since a placeholder in this vocabulary is the entry's own text, drawn by
the entry, while a select's is content drawn in the field's place.

### 2. Each part states its look on the box around the content

Where the component drew `new Text(Label, TypeRole.Label, theme.TextSecondary)`, it now wraps the developer's node
in a box whose style states `Content = new ContentStyle { Role = TypeRole.Label }` and
`Foreground = theme.TextSecondary`, so a bare `Text` or a `string` is drawn exactly as before, and a node that names
its own look keeps it. The pins with a plain string are the proof: the output does not move.

### 3. The names come from #1916's part, never from a string the component gathers

A field's entry needs a name and a description that are strings today (`TextEntry.Label`, `.Description`). With
the title a node, the component cannot read the words out of it at its build: a title that is a component of the
developer's own is built later, and its words would be lost without a sound. #1916's part marks the title as
naming, and the helper or error as describing, the nearest nameable around it, and the realizers resolve it after
the tree is built: an id and `aria-labelledby` or `aria-describedby` on the web, several in reading order, and the
text gathered under the part on Photon. The prefix and suffix are marked as naming parts after the title, so the
name reads as the field draws it.

Rejected: a helper that gathers the text under a node at the component's build (`VisualNode.TextOf`). It misses a
component handed as the title, which is the case the change exists for.

### 4. Delivered in the order its dependencies land

The parts that need no naming go first: `Select`'s hint (the field's name is its content, as today), `FileUpload`'s
title and hint (the area is no control; its button names itself), and `MoneyInput`'s and `NumberInput`'s style.
The titles, helpers and errors follow on #1916's part, in the same pull request.

## Risks / Trade-offs

- **A title whose node carries no text** (an icon alone) names nothing. The developer gives the icon its own
  label, as an icon anywhere takes one; the guard of #1916 reports a nameable with no name where it can.
- **Every call site that named a renamed parameter moves at once**, in the samples, the tests and the wiki's five
  languages, in this pull request: a preview break, with one migration line per name.
