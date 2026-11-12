## ADDED Requirements

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

### Requirement: A shipped theme leaves a role's colour to the component

`PhotonTheme` and `MaterialTheme` SHALL leave null every colour of a slot that would equal one of
their own roles, the Button's slot included, so the component reads the role itself and a theme built
from them that recolours the role recolours the component.

#### Scenario: A decorator recolours a role

- **WHEN** a theme wraps `PhotonTheme`, forwards its slots, and answers `TextMuted` with green
- **THEN** an unselected tab's label, a calendar's weekday names and an empty state's icon are green

#### Scenario: No slot copies a role

- **WHEN** every colour of every slot `With` takes is read under `PhotonTheme` and `MaterialTheme`
- **THEN** none of them equals one of the theme's roles
