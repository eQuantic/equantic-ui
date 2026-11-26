## MODIFIED Requirements

### Requirement: An input or a picker takes its family's record as its style

Each of `TextInput`, `SearchField`, `Select`, `FormInput`, `NumberInput`, `MoneyInput`, `FileUpload`, `DatePicker`,
`TimePicker` and `DateTimePicker` SHALL take `style`, an `InputTheme`; `Calendar` SHALL take a `CalendarTheme`;
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

#### Scenario: A typed input hands its style to its input

- **WHEN** a `NumberInput` or a `MoneyInput` is given an `InputTheme` with a border colour
- **THEN** the `TextInput` it draws takes that border, and a sibling with no style keeps the theme's

## ADDED Requirements

### Requirement: An input or a picker takes the developer's content as nodes

`TextInput`, `FormInput`, `NumberInput`, `MoneyInput`, `DatePicker`, `TimePicker` and `FileUpload` SHALL take
their title as a `VisualNode` named `title`, and `DateTimePicker` its two as `dateTitle` and `timeTitle`. The
fields SHALL take `helper` and `error` as nodes, `Select` SHALL take `hint` as a node for what its closed field
shows before a choice, and `FileUpload` SHALL take `hint` as a node. Each part SHALL hand its content the look the
component drew its text in (the role, the line limit, the colour), so a `string` or a bare `Text` SHALL be drawn as
the component's own text was, and a node that names its own look SHALL keep it.

#### Scenario: A string title draws as before

- **WHEN** a `TextInput` is given `title: "Email"`
- **THEN** the title is drawn in the `Label` role and the secondary colour, as the label string was, on the web and
  on Photon

#### Scenario: A select's hint with an icon

- **WHEN** a `Select` with no choice is given `hint: Row(children: [Icon(Icons.Search), "Choose a country"])`
- **THEN** its closed field draws the row in the field's role and the muted colour, and the field is named by it

#### Scenario: A select with no hint

- **WHEN** a `Select` with no choice is given no hint
- **THEN** its closed field shows the SDK's localized word for choosing, in the app's culture

### Requirement: A field's title names it and its helper describes it

A field's title SHALL name its entry, and its helper or error SHALL describe it, through the part that names or
describes the nearest nameable, resolved by the realizers after the tree is built, so a title that is a component
of the developer's own SHALL still name the field. The prefix and suffix SHALL be named after the title, in reading
order.

#### Scenario: A title that is a component

- **WHEN** a `TextInput` is given a title that is an app's own component drawing the words "Billing email"
- **THEN** a screen reader on the entry hears "Billing email", on the web and on Photon

#### Scenario: An error describes the field

- **WHEN** a `TextInput` is given `error: "Required"`
- **THEN** the entry is described by "Required", and the line below it announces it once
