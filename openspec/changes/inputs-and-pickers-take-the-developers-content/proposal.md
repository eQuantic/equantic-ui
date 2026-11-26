## Why

Closes #1609, the content half of #1548, a family slice of #1545 (the catalog takes the developer's content,
builders and style), whose design is #1580's
(`openspec/changes/archive/2026-11-16-the-catalog-takes-the-developers-content-and-style`, decisions 1 to 4 and 7).
The family's style and builder half is #1608. This half waited for #1577, which made a string a node and hands
content the look its component resolved, and it takes the names of its fields from #1916, the part that names
or describes the nearest nameable around it.

A developer who wants a field's label with an icon beside it, a helper line with a link, or an upload area that
invites in the app's own words and picture cannot say so today: the inputs and pickers take their label, helper,
error, hint and invitation as strings, drawn the one way the component draws text. And the two typed inputs that
came after #1608 started, `MoneyInput` and `NumberInput` (#1768), take no style of their own.

## What Changes

- **Content is a node.** Where the family draws what the developer hands it, the parameter is a `VisualNode`,
  named as decision 1 names it. A `string` still converts to a node (#1577), so a call that passed a string reads
  as it did; one that named the parameter writes its new name:
  - `label:` becomes `title:` on `TextInput`, `FormInput`, `NumberInput`, `MoneyInput`, `DatePicker`,
    `TimePicker` and `FileUpload`; `dateLabel:` and `timeLabel:` become `dateTitle:` and `timeTitle:` on
    `DateTimePicker`. `Label` is the accessible name in this vocabulary and never content (decision 1).
  - `helper:` and `error:` keep their names on `TextInput`, `FormInput`, `NumberInput` and `MoneyInput`, and
    take a node.
  - `placeholder:` becomes `hint:` on `Select`, Flutter's `DropdownButton.hint`: what the closed select shows
    before a choice, a node. Its default is the SDK's own localized word, where it was an English literal.
  - `hint:` on `FileUpload` takes a node.
  - `LabelHidden` becomes `TitleHidden` on `TextInput` and `DatePicker`.
- **The content takes the part's look.** Each part states the look it hands its content (#1577's
  `BoxStyle.Content` and `Foreground`): the title in the `Label` role, the helper and error in `Caption` on one
  line in the muted or the destructive colour, the hint in the field's role and muted colour. A bare `Text` or a
  `string` is drawn as the component drew its text; a node that names its own look keeps it.
- **The names come from the content.** A field's title names its entry and its helper or error describes it,
  through #1916's part, which the realizers resolve after the tree is built: an id and `aria-labelledby` or
  `aria-describedby` on the web, the text gathered under the part on Photon. So a title the developer hands as a
  component of their own still names the field. The prefix and suffix stay text (#1761) and are named with the
  title in reading order.
- **Text stays text where it is text by nature**: a field's value, its placeholder (drawn by the entry itself),
  its prefix and suffix, an option's or a segment's name, a group's heading, a culture's names, a currency's code
  and symbol, a number's unit. Those entries stay in the family's baseline with their reasons.
- **`MoneyInput` and `NumberInput` take `style:`**, an `InputTheme`, handed to the `TextInput` they draw, as
  `FormInput` hands its own.

What a developer writes:

```csharp
TextInput(email, onChanged: SetEmail,
    title: Row(gap: Space.S1, children: [Icon(Icons.Mail), "Email"]),
    helper: Row(children: ["We never share it. ", Link("/privacy", "Privacy")]))

Select(countries, selected, onChanged: Choose, hint: "Choose a country")
```

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `catalog-customization`: the inputs and pickers take their title, helper, error and hint as nodes, named
  through the part that names their field; `MoneyInput` and `NumberInput` take their family's style.

## Impact

- **Components** (`eQuantic.UI.Components`): `TextInput`, `FormInput`, `NumberInput`, `MoneyInput`, `Select`,
  `DatePicker`, `TimePicker`, `DateTimePicker`, `FileUpload` and their factories in `UI.cs`. The public surface
  moves: each widened constructor declares its new signature and retires its old one in
  `PublicAPI.Unshipped.<branch>.txt`, as a preview allows.
- **Every call site in the tree**: the samples, the templates, the tests and the wiki's snippets that named
  `label:`, `placeholder:` on a select, `dateLabel:` or `timeLabel:`.
- **Runtime**: the components' transpiled twins, regenerated; the SDK's new localized hint in the string catalog
  and its TypeScript twin. No runtime code is written by hand.
- **Web realizer, Photon, eqc, the SDKs**: untouched by this change; the naming part is #1916's.
- **The guard**: the family's content entries and the two `style` entries leave
  `inputs-and-pickers.baseline.txt`; the entries kept as text stay with their reasons.
- **Docs**: the wiki's Components and Upgrading pages in every language they are written in; a ledger entry.

A break, in preview. One migration line per renamed parameter: `label:` → `title:`; `dateLabel:` →
`dateTitle:`; `timeLabel:` → `timeTitle:`; `Select(placeholder:)` → `Select(hint:)`; `LabelHidden` →
`TitleHidden`. A call that passed these positionally, or a string to `helper:` or `error:`, compiles unchanged.

## Principles

- **1, never a platform artifact**: a field's label with an icon is C#, not markup around the field.
- **4, written once**: the components are shared, so both targets take the content at once.
- **6, Flutter**: `InputDecoration`'s `label`, `helper` and `error` widgets and `DropdownButton.hint`, with the
  names decision 1 gives them; the naming part is `Semantics(namesRoute:)`'s shape, widened to a field.
- **13, break freely in preview**: the renamed parameters replace the old ones, with no alias kept.
