## Why

Closes #1607, the content half of #1547, a family slice of #1545 (the catalog takes the developer's content,
builders and style), whose design is #1580's
(`openspec/changes/archive/2026-11-16-the-catalog-takes-the-developers-content-and-style`, decisions 1 to 4 and 7).
The family's style and builder half is #1606. This half waited for #1577, which made a string a node and hands
content the look its component resolved. The group heading takes the group's name from #1916, the part that names
or describes the nearest nameable around it.

A developer who wants a button whose words carry a badge, a chip with a person's picture before the name, or a
checkbox whose text holds a link to the terms cannot say so today. The buttons, chips and selection controls take
what they show as a string, drawn the one way the component draws text.

## What Changes

- **Content is a node.** Where the family draws what the developer hands it, the parameter is a `VisualNode`,
  named as decision 1 names it. A `string` still converts to a node (#1577), so a call that passed a string reads
  as it did, and one that named the parameter writes its new name:
  - `label:` becomes `child:` on `Button`, `FormSubmit` and `Chip` (Flutter's `ElevatedButton.child`, and its
    `Chip.label`, which decision 1 renames because `Label` is the accessible name in this vocabulary);
  - `label:` becomes `title:` on `Checkbox`, the text beside the box (Flutter's `CheckboxListTile.title`), and on
    `RadioGroup`, the group's heading.
- **A chip takes an avatar.** `Chip` gains `avatar:`, a node drawn before its child (Flutter's `Chip.avatar`).
  A selected filter chip draws its check in the avatar's place, as Flutter's `FilterChip` does.
- **The content takes the component's look.** Each component states the look it hands its content (#1577's
  `BoxStyle.Content` and `Foreground`):
  - a button's child takes the size table's label style on one line, in the variant's colour;
  - a chip's child takes the chip's label style on one line, and its avatar the chip's icon size;
  - a checkbox's title takes `BodyM` on two lines in the primary colour, or the muted one when disabled;
  - a group's heading takes `Caption` on one line in the muted colour.

  A bare `Text` or a `string` is drawn as the component drew its text, and a node that names its own look keeps it.
- **The names come from the content.** A button's, a chip's and a checkbox's content sits inside the control, so the
  control is named by its words, as both targets already name a control with no label (decision 4). A group's
  heading names the group through #1916's part, so a heading that is a component of the developer's own still names
  it.
- **Text stays text where it is text by nature.** The entries kept as text stay in the family's baseline with their
  reasons:
  - an icon button's, a switch's, a slider's and a progress bar's accessible name;
  - the value in words that a reader announces;
  - a radio option's name, which the option builder draws (#1606).

What a developer writes:

```csharp
Button(Row(gap: Space.S2, children: ["Inbox", Badge(count: 3)]), onPressed: Open)

Chip("Ana Souza", avatar: Avatar("AS"), kind: ChipKind.Input, onRemove: Remove)

Checkbox(accepted, onChanged: Toggle,
    title: Row(children: ["I accept the ", Link("/terms", "terms")]))
```

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `catalog-customization`: the buttons, chips and selection controls take their content as nodes, a chip takes an
  avatar, and a group's heading names its group through the part that names the nearest nameable.

## Impact

- **Components** (`eQuantic.UI.Components`): `Button`, `FormSubmit`, `Chip`, `Checkbox`, `RadioGroup` and their
  factories in `UI.cs`. The public surface moves: each widened constructor declares its new signature and retires
  its old one in `PublicAPI.Unshipped.<branch>.txt`, as a preview allows.
- **Every call site in the tree**: the components that build a `Button` or a `Chip` themselves (a dialog's actions,
  an empty state's call to action), the samples, the templates, the tests and the wiki's snippets that named
  `label:` or read the `Label` of one of the five.
- **Runtime**: the components' transpiled twins, regenerated. No runtime code is written by hand.
- **Web realizer, Photon, eqc, the SDKs**: untouched by this change. A control with no label is named by its content
  on both targets already, and the group's naming part is #1916's.
- **The guard**: the family's five content entries leave `buttons-selection-and-progress.baseline.txt`, and the
  entries kept as text stay.
- **#1606**: the style half edits the same constructors and the same baseline. Whichever lands second merges `main`
  and keeps both, the node first and `style:` last.
- **Docs**: the wiki's Components and Upgrading pages in every language they are written in, and a ledger entry.

A break, in preview. One migration line per renamed parameter:
- `Button(label:)`, `FormSubmit(label:)` and `Chip(label:)` become `child:`;
- `Checkbox(label:)` and `RadioGroup(label:)` become `title:`;
- a reader of `Button.Label`, `FormSubmit.Label` or `Chip.Label` reads `Child`, and one of `Checkbox.Label` or
  `RadioGroup.Label` reads `Title`.

A call that passed a string positionally compiles unchanged.

## Principles

- **1, never a platform artifact**: a button with a badge in it is C#, not markup around the button.
- **4, written once**: the components are shared, so both targets take the content at once.
- **6, Flutter**: `ElevatedButton.child`, `Chip.avatar`, `CheckboxListTile.title`, with the names decision 1 gives
  them.
- **13, break freely in preview**: the renamed parameters replace the old ones, with no alias kept.
