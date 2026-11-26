## ADDED Requirements

### Requirement: An input or a picker takes its family's record as its style

Each of `TextInput`, `SearchField`, `Select`, `FormInput`, `FileUpload`, `DatePicker`, `TimePicker` and
`DateTimePicker` SHALL take `style`, an `InputTheme`; `Calendar` SHALL take a `CalendarTheme`;
`SegmentedControl`, `Stepper` and `CultureSwitcher` SHALL take a `SegmentedTheme`. The component SHALL lay the record over its
theme's slot with the record's `Merge`, so a member the instance sets wins and a member it leaves null is the
theme's. A component that composes another SHALL hand its style down. With no style, the output SHALL be what
it was.

#### Scenario: A field with the brand's border

- **WHEN** a theme's input border is gray and a `TextInput` is given `style: new InputTheme { BorderColor = Brand }`
- **THEN** that field's border is the brand's color, on the web and on Photon, and every other field's stays gray

#### Scenario: A form field hands its style to its input

- **WHEN** a `FormInput` is given an `InputTheme` with a fill
- **THEN** the `TextInput` it draws takes that fill

### Requirement: A calendar picker takes its panel's record beside its field's

`DatePicker` and `DateTimePicker` SHALL take `calendarStyle`, a `CalendarTheme`, laid over the theme's calendar
slot for the panel they open, beside `style` for the field.

#### Scenario: A date picker's panel in another fill

- **WHEN** a `DatePicker` is given `calendarStyle: new CalendarTheme { PanelFill = Surface }` and opened
- **THEN** its panel is drawn in that fill, and its field keeps the theme's input look

### Requirement: A select and a segmented control draw their parts with a builder

`Select` SHALL take `optionBuilder`, and `SegmentedControl` SHALL take `segmentBuilder`, each a
`Func<int, VisualNode>` over the part's index. The component SHALL draw the builder's node as the part's
content inside the part's own chrome, and the part's text SHALL stay its accessible name. A select's index SHALL
be the one `selectedIndex` and `onChanged` count, the ungrouped options first and then each group's in order.

#### Scenario: Options with a flag

- **WHEN** a `Select` of three currencies is given an `optionBuilder` that answers a row with a flag and the
  currency's name, and is opened
- **THEN** each option shows that row, its accessible name is the currency's name, and choosing the second
  calls `onChanged(1)`

#### Scenario: A grouped select counts across its groups

- **WHEN** a `Select` has one ungrouped option and a group of two, and an `optionBuilder`
- **THEN** the builder is asked for indexes 0, 1 and 2, the group's options being 1 and 2
