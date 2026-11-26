## Context

#1580 decided how every family of the catalog takes the developer's content, builders and style (its design,
decisions 1 to 9) and left the families to slices. This one is #1608, #1548's style and builder halves; the content
half is #1609, which waits for #1577, which makes a string a node and hands content the look its component resolved.

## Decisions

### The style is merged once, where the component read the theme

Every component reads its slot in one place today (`var slot = theme.Input;`), and the merge replaces that
read: `var slot = InputTheme.Merge(theme.Input, Style);`. Everything the component derives from the slot then
follows the instance's record, with nothing else changed. A component that read no slot of its own
(`FormInput`, `CultureSwitcher`, `DateTimePicker`) composes one that does, and hands its style down.
`CultureSwitcher` composes a `SegmentedControl` for three languages or fewer and a menu on a button past
that, never a field, so its record is `SegmentedTheme`, handed to the segments; the menu takes the menu's
own theme, which a style of the switcher does not reach. A component
that draws something the record names but read it from the theme's tokens until now (`FileUpload`'s border and
fill) reads the merged slot first and the tokens after, so a style it is handed reaches what it draws.

### Two records for the pickers whose panel is a calendar

`DatePicker` and `DateTimePicker` are a field and a calendar. Decision 6 makes the record the whole instance
style, and no record holds both looks, so they take `style:` (`InputTheme`, the field, which the guard reads)
and `calendarStyle:` (`CalendarTheme`, the panel). Merging the two records into one for the pickers would be a
record that no theme slot answers, which decision 6 rejects.

### The builders draw content, and the text stays the part's name

A builder answers the content of a part by its index; the component wraps it in the part's chrome, as it wraps
its own text today. The text the component was given stays the part's accessible name (decision 4): a select's
option keeps its name for the screen reader, the typeahead and the closed field, which shows the chosen
option's text, as Flutter's `DropdownButton` shows its selected item.

A select's index is the one `selectedIndex` and `onChanged` already count, across the ungrouped options and
then each group, so one function answers every option whether or not the select is grouped.

## Risks / Trade-offs

- **A builder's content that takes more room than the text did** is laid out by the part as any child is; the
  option rows and the segments keep their heights from the slot, as they do for text.
- **The conformance and variant pins** must show the same output with no style and no builder: the merge of a
  null record is the theme's own, and a missing builder draws the text as before.
