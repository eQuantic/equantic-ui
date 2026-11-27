# catalog-customization Specification

## Purpose
What a component of the catalog owes a developer who customizes it: its content as a node, a builder for
what it repeats and its family's slot record as its own style (CLAUDE.md, "Every component is composed, and
takes the developer's content, builders and style"). This capability holds the guard that keeps the catalog
to it: what still takes text, and which components still lack a style, is a list per family that only
shrinks. The family slices add the requirements of each family's content, builders and style here.

## Requirements

### Requirement: The catalog's text content is a list that only shrinks

`CatalogCustomizationTests` SHALL fail when a component of the catalog, or an item type of the catalog that a
component takes through a constructor parameter or a property, has a member that carries text and that no
family's file under `tests/eQuantic.UI.Web.Tests/Coverage/catalog-customization/` lists. A member carries text
when it is a settable property the catalog declares, or a constructor parameter that no settable property of
its name carries, and its type is a `string`, a sequence of text, or a delegate that answers text. The failure
SHALL name the member and the family's file it belongs in. A member that takes a `VisualNode`, or a builder
that answers one, SHALL never be listed.

#### Scenario: A component gains a text property

- **WHEN** `Card` gains `public string? Footer { get; init; }` and no family's file lists `Card.Footer`
- **THEN** the test fails naming `Card.Footer  text` and `lists-and-tables.baseline.txt`

#### Scenario: A member that takes a node

- **WHEN** a component takes `VisualNode child` and `Func<int, VisualNode> rowBuilder`
- **THEN** the census names neither

### Requirement: Every component takes a style of its own, or is listed

The test SHALL fail when a component of the catalog has no constructor parameter `style` and property `Style` of
one type that is a slot record of the theme (a type `IAppTheme` answers, with a static `Merge` over two of it),
and no family's file lists the component's `style` entry. A `Style` the constructor cannot reach SHALL count as
none, because the factory mirrors the constructor.

#### Scenario: The family's record as the instance's style

- **WHEN** a component's constructor takes `ButtonTheme? style` and its `Style` property is a `ButtonTheme?`
- **THEN** the census lists no `style` entry for it

#### Scenario: A style only an initializer reaches

- **WHEN** a component has `public MarkdownTheme? Style { get; init; }` and no constructor parameter `style`
- **THEN** the census lists its `style` entry

### Requirement: Every component belongs to one family

Each family's file SHALL name, on `# owns:` lines, the components it holds and the item types that carry text
through them. The test SHALL fail when a component of the catalog, or an item type with an entry, is owned by no
family or by more than one, when an entry sits in a file that does not own its type, and when a file owns a name
that is neither a component nor an item that carries text.

#### Scenario: A new component nobody placed

- **WHEN** the catalog gains a component `Gauge` that no `# owns:` line names
- **THEN** the test fails naming `Gauge`, before it reads any entry

#### Scenario: An entry in another family's file

- **WHEN** `Card  style` is moved from `lists-and-tables.baseline.txt` to `navigation.baseline.txt`
- **THEN** the test fails naming `navigation.baseline.txt` and `Card  style`

### Requirement: Every entry is decided, and the list never grows by itself

An entry's reason SHALL name the issue that removes it (`#N`) or begin with `kept:` and a reason; any other
reason SHALL fail. An entry the census no longer measures SHALL fail until the list is regenerated. Regenerating
(`EQ_UPDATE_CATALOG_CUSTOMIZATION=1`) SHALL remove such entries, and owned names that are no longer measured,
keep every other line as written and where it was, and never add an entry.

#### Scenario: An undecided reason

- **WHEN** an entry's reason is `not looked at yet`
- **THEN** the test fails naming the entry

#### Scenario: A member that took a node

- **WHEN** a file lists `Card.Ghost  text  #1551` and `Card` has no such member
- **THEN** the test fails naming it, and regenerating removes that line and leaves the file otherwise as it was

### Requirement: The scan reaches every library that ships a component

The catalog SHALL be derived from the build output: every assembly beside the test that references the
vocabulary and ships a public, non-abstract `UiComponent`. The test SHALL fail when the scan does not reach
`eQuantic.UI.Components`, `eQuantic.UI.Charts` and `eQuantic.UI.Spreadsheets`, or finds 55 components or fewer.

#### Scenario: The libraries of today

- **WHEN** the test runs on main
- **THEN** the scan reaches the three libraries and more than 55 components

### Requirement: Buttons, selection controls and progress take their family's style

`Button`, `FormSubmit`, `IconButton`, `Chip`, `Checkbox`, `RadioGroup`, `Switch`, `Slider`, `ProgressBar`,
`PullToRefresh` and `Skeleton` SHALL each take a constructor parameter `style` and a property `Style` of their
family's slot record (`ButtonTheme` for the first two, `IconButtonTheme`, `ChipTheme`, `SelectionControlsTheme`
for the four selection controls, `ProgressTheme` for the last three), and their factories SHALL mirror it. Each
SHALL resolve its slot as the record's `Merge` of the theme's slot under its own `Style`, on the web, in the
browser's twin and on Photon alike: a member the instance names wins, and a member it leaves null is the
slot's. A `ProgressBar` retained across its parent's builds SHALL adopt the style its parent hands it.

#### Scenario: The instance's member wins over the slot's

- **WHEN** a theme's `SelectionControls` slot names a checkbox box of 29 and a `Checkbox` is built with
  `style: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 33 } }`
- **THEN** the checkbox's box is 33 wide on the web and on Photon, and no box of it is 29 wide

#### Scenario: A member the instance leaves null is the slot's

- **WHEN** a theme's slot names a radio ring of 29 and a `RadioGroup` is built with a style that names only its
  dot, 13
- **THEN** the group draws a ring of 29 and a dot of 13

#### Scenario: The submit button hands its style on

- **WHEN** a `FormSubmit` is built with `style: new ButtonTheme { MinWidth = 191 }` under a theme whose `Button`
  slot names 173
- **THEN** the button it builds is at least 191 wide, and nothing is 173

#### Scenario: A retained bar adopts a new style

- **WHEN** a `ProgressBar` built with a bar height of 7 adopts a configuration whose style names 11
- **THEN** its next build draws a bar 11 tall

### Requirement: A radio group's options take a builder

`RadioGroup` SHALL take `optionBuilder`, a `Func<int, VisualNode>` over an option's index, which draws what the
option shows in place of its name. The group SHALL keep its chrome: the ring, the row a press lands on, the one
Tab stop, and each option's name in `options` as that option's accessible name. A content that names no colour
SHALL take the option's colour, the primary text, or the muted text while the group is disabled. With no
builder, the group SHALL draw each option's name, as before.

#### Scenario: The builder draws, the name stays the radio's name

- **WHEN** a `RadioGroup(["Standard", "Express"], …, optionBuilder: i => new Text(shown[i]))` is drawn
- **THEN** the drawn texts are the builder's and not the names, and the radios a reader meets are named
  "Standard" and "Express", on the web and on Photon

#### Scenario: The content takes the option's colour

- **WHEN** the same group is disabled
- **THEN** the builder's text, which names no colour, is drawn in the theme's muted text

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
