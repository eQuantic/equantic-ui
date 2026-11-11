# theme-slots Specification

## Purpose
What makes a component look like itself is the theme's to say. A theme answers a slot per component
family (`docs/ELEMENT-TREE-PLAN.md` section 3.10), each a record of data whose members are all optional,
and a component resolves every value it draws from its slot, the theme's control ladder and its own
default, in that order, on the web and on Photon alike. The slots cross to the browser with the rest of
the theme, so a page keeps them after it hydrates.

## Requirements

### Requirement: A component resolves its look from the theme's slots in one order

Each value a sized control draws SHALL resolve, the first that answers winning, from its family's slot
in the theme, then the theme's `Controls` slot, then the component's default, and a value that varies
by density SHALL be read at the context's density at every step. A slot SHALL be data only, every
member optional, and a theme that answers no slot SHALL draw every control as before.

#### Scenario: A theme changes the medium height

- **WHEN** an app's theme answers a `Controls` slot whose medium height is 52 comfortable and 44 compact
- **THEN** every medium control is that tall at that density, on the web and on Photon

#### Scenario: The design system's own theme

- **WHEN** a page renders under `PhotonTheme`
- **THEN** it renders byte for byte as it did before the slots, since its slots are the ladder

### Requirement: A theme is built from another with one slot replaced

`theme.With(…)` SHALL return a theme with the slots it is given and every other member of the theme it
wraps, the interface's defaults included, and a further `With` SHALL keep the slots already replaced.

#### Scenario: A theme from Photon with its own button

- **WHEN** an app selects `PhotonTheme.Instance.With(button: new ButtonTheme { MinWidth = 88 })`
- **THEN** its buttons never shrink below 88, and every other member answers what `PhotonTheme` answers

### Requirement: Every member of the theme reaches the browser

The theme's JSON SHALL carry every member of `IAppTheme`: each slot member by member when the theme
answers it, and the code palette, every kind's colour. A member of the interface with no key in it
SHALL fail the build.

#### Scenario: A theme with its own code palette

- **WHEN** a theme overrides `Code` and a code block renders again in the browser
- **THEN** it is coloured by that palette, as the server and Photon colour it

### Requirement: Navigation and collections read their family's slot

Tabs, BottomNavigation, NavigationRail, AppBar, Breadcrumb, Pagination and PageIndicator SHALL read
the theme's `Navigation` slot; ListItem, List, Divider, Accordion and SwipeableRow its `List` slot;
Table and DataTable its `Table` slot; Badge, Avatar and EmptyState its `Badge` slot; Calendar and
DatePicker its `Calendar` slot. Each value SHALL resolve from the slot, then the component's default,
and a theme that answers none of them SHALL draw every one of these components as before.

#### Scenario: A theme with taller tabs

- **WHEN** an app's theme answers a `Navigation` slot whose tab height is 60
- **THEN** its tab strips are 60 tall, on the web and on Photon

#### Scenario: Material 3's navigation bar

- **WHEN** a page renders a BottomNavigation under `MaterialTheme`
- **THEN** the bar is 80 tall and each destination's indicator 64 by 32, as Material 3 specifies

#### Scenario: The design system's own theme

- **WHEN** a page renders these components under `PhotonTheme`
- **THEN** it renders byte for byte as it did before their slots

### Requirement: A document reads its own style ahead of the theme's

A Markdown SHALL resolve each member of its style from its own `Style`, then the theme's `Markdown`
slot, then its default, member by member, so a document that names one member keeps the theme's for
the rest.

#### Scenario: A document that names its gap

- **WHEN** the theme's `Markdown` slot names a block gap of 23 and a table radius of 17, and a
  Markdown's own `Style` names a block gap of 31
- **THEN** its blocks are 31 apart and its tables keep the radius of 17
