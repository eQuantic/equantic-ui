## Why

Closes #1608, the style and builder half of #1548, a family slice of #1545 (the catalog takes the developer's
content, builders and style), whose design is #1580's
(`openspec/changes/archive/2026-11-16-the-catalog-takes-the-developers-content-and-style`, decisions 5 to 9).
The family's content half, the label, helper and error as nodes, is #1609, which waits for #1577.

A developer who wants one field drawn otherwise than the theme draws every field (a search box with the
brand's border, a select whose options carry an icon) cannot say so today: the inputs read `theme.Input`, the
pickers `theme.Calendar`, the segmented controls `theme.Segmented`, and nothing else. A theme can restyle
every field at once; one field cannot restyle itself, and an option is a string drawn the one way the select
draws it.

## What Changes

- **Style.** Every component of the family takes `style:`, its family's slot record, laid over the theme's
  with the record's `Merge`, the first step of §3.10's order, as `Markdown` already does with `MarkdownTheme`:
  - `InputTheme`: `TextInput`, `SearchField`, `Select`, `FormInput`, `FileUpload`, `DatePicker`,
    `TimePicker`, `DateTimePicker`;
  - `CalendarTheme`: `Calendar`;
  - `SegmentedTheme`: `SegmentedControl`, `Stepper`, and `CultureSwitcher`, which draws a segmented control
    for three languages or fewer and a menu past that, never a field.
  The two pickers whose panel is a calendar (`DatePicker`, `DateTimePicker`) take `calendarStyle:` too, a
  `CalendarTheme` for the panel, since one record cannot carry both looks. A component that composes another
  hands its style down: `FormInput` to its `TextInput`, `CultureSwitcher` to its segmented control, `DateTimePicker`
  to its two pickers.
- **Builders.** What the family repeats is drawn by a builder over the part's index, `Func<int, VisualNode>`:
  - `Select(optionBuilder:)` draws an option's content, by the option's index in the list `selectedIndex` and
    `onChanged` count (the ungrouped options, then each group's in order). The option's text stays its
    accessible name, its typeahead key and what the closed select shows.
  - `SegmentedControl(segmentBuilder:)` draws a segment's content; the segment's text stays its accessible
    name.
  The component keeps its chrome (the selection mark, the thumb, the hover and the pressed fill), as decision 5
  says. `Stepper` is the numeric control, a value between two buttons, and repeats nothing, so it takes no
  builder.
- All of it is optional named constructor parameters, mirrored by the factories (decision 7), and carried by
  `AdoptConfig` where the component is stateful.

What a developer writes:

```csharp
SearchField(query, onChanged: Search, style: new InputTheme { BorderColor = Brand.Accent, SearchRadius = 8 })

Select(currencies, selected, onChanged: Choose,
    optionBuilder: i => Row(gap: Space.S2, children: [Icon(Flags[i]), Text(currencies[i])]))
```

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `catalog-customization`: the inputs and pickers take their family's record as their style, the two
  calendar pickers their calendar's beside it, and a select and a segmented control draw their parts with
  the developer's builder.

## Impact

- **Components** (`eQuantic.UI.Components`): the twelve components and their factories in `UI.cs`. Each
  widened constructor and the factory that mirrors it declare their new signature and retire their old one
  in `PublicAPI.Unshipped.<branch>.txt`, as a preview allows.
- **Runtime**: the components' transpiled twins, regenerated. No runtime code is written by hand.
- **Web realizer, Photon, eqc, the SDKs**: untouched; the components are written once.
- **The guard**: the family's `style` entries leave `inputs-and-pickers.baseline.txt`; its `text` entries
  stay for the content half.
- **Docs**: the wiki's Components page in every language it is written in; a ledger entry.

A break, in preview: each widened constructor's old shape goes. An app that calls a component positionally
past its last parameter writes the new parameter by name; named arguments compile unchanged.

## Principles

- **1, never a platform artifact**: a field restyles itself in C#, with the record its theme already speaks.
- **4, written once**: the components are shared, so both targets take the style and the builders at once.
- **6, Flutter**: a builder over the index is `ListView.builder`'s and Flutter's `DropdownMenuItem` child, and
  a per-instance style record is Material 3's `ButtonStyle` shape, as decision 6 says.
- **13, break freely in preview**: the widened constructors replace the old ones, with no overload kept.
