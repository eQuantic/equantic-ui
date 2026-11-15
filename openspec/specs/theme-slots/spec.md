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

### Requirement: A theme is built from another with a slot laid over its own

`theme.With(…)` SHALL return a theme whose every slot it is given is laid over the wrapped theme's, member
by member: a member the given slot names SHALL win, a member it leaves null SHALL be the wrapped theme's,
and a member holding a slot record SHALL merge the same way. Every other member of the wrapped theme SHALL
be forwarded, the interface's defaults included, and a further `With` SHALL keep the slots already laid
over. Every slot record SHALL carry `Merge(under, over)`, the member-wise merge `With` uses, in C# and in
its TypeScript twin.

#### Scenario: A theme from Photon with its own button

- **WHEN** an app selects `PhotonTheme.Instance.With(button: new ButtonTheme { MinWidth = 88 })`
- **THEN** its buttons never shrink below 88, and every other member answers what `PhotonTheme` answers

#### Scenario: One member of a slot the design system fills

- **WHEN** an app selects `MaterialTheme.Instance.With(button: new ButtonTheme { MinWidth = 88 })`
- **THEN** its buttons never shrink below 88, and keep Material 3's label weight, link padding and ladder

### Requirement: Every member of the theme reaches the browser

The theme's JSON SHALL carry every member of `IAppTheme`: each slot member by member when the theme
answers it, and the code palette, every kind's colour. A member of the interface with no key in it
SHALL fail the build.

#### Scenario: A theme with its own code palette

- **WHEN** a theme overrides `Code` and a code block renders again in the browser
- **THEN** it is coloured by that palette, as the server and Photon colour it

### Requirement: The form controls read their family's slot

`IAppTheme` SHALL answer five slots that answer null by default:
- `IconButton`, which styles the IconButton;
- `Input`, which styles TextInput, SearchField, Select and TimePicker;
- `SelectionControls`, which styles Checkbox, RadioGroup, Switch and Slider;
- `Segmented`, which styles SegmentedControl and Stepper;
- `Chip`, which styles the Chip.

Every member of each slot SHALL be nullable data. Each component SHALL resolve every value of its look in the order of the theme slots:
- its family's slot first;
- then, for a number of the control ladder, the theme's `Controls`;
- then the component's own default.

A value that varies by density SHALL be read at `context.Density` at every step.

#### Scenario: A slot reaches the web's lowering

- **WHEN** a page under `PhotonTheme.Instance.With(selectionControls: new SelectionControlsTheme { Box = new DensityPair { Comfortable = 30 } })` holds a `Checkbox`
- **THEN** the checkbox's box lowers 30 wide and 30 tall on the web

#### Scenario: A slot reaches Photon's layout

- **WHEN** the same page is laid out on Photon
- **THEN** the checkbox's box is laid out 30 by 30

#### Scenario: A member left null is the component's own

- **WHEN** a theme answers `SelectionControls` with only `Box` named
- **THEN** the checkbox's border and corner are the ones it draws with no slot

#### Scenario: The density picks its side

- **WHEN** the slot's `Box` is `new DensityPair { Comfortable = 30, Compact = 24 }` and the page is compact
- **THEN** the checkbox's box is 24 by 24

### Requirement: The SDK's themes fill the form control slots

`PhotonTheme` SHALL answer the five form control slots with the values its components drew before the slots existed, so that every pinned output stays byte for byte. `MaterialTheme` SHALL answer them from Material 3's component specifications.

#### Scenario: Photon draws what it drew

- **WHEN** the transpiled pins, the parity fixture and the realizers' goldens are produced under `PhotonTheme`
- **THEN** they are byte for byte what they were before the slots

#### Scenario: Material draws its own text field

- **WHEN** a `TextInput` is lowered under `MaterialTheme`
- **THEN** its container is 56dp tall and its outline is 1dp, and 2dp while focused

### Requirement: The form control slots cross to the browser

Every member a form control slot names SHALL cross to the browser in the theme the server writes into the shell, under its camel-cased name. The browser SHALL read the slot in the same order as the server.

#### Scenario: A slot member crosses

- **WHEN** the server's theme answers `Chip` with `PaddingX = 20`
- **THEN** the theme the browser builds answers `chip.paddingX` as 20, and a chip the browser renders again is padded by 20

### Requirement: The surfaces, menus, notices, progress and cards read their theme slots

The Dialog, the BottomSheet and the Drawer SHALL read `IAppTheme.Modal`, the Popover, the Menu and the
Tooltip `IAppTheme.Menu`, the Toast, the Banner and the cookie consent card `IAppTheme.Notice`, the
ProgressBar, PullToRefresh and the Skeleton `IAppTheme.Progress`, and the Card `IAppTheme.Card`, in the
slot order, on the web and on Photon. A member a slot leaves null SHALL be the component's own value.

#### Scenario: A value in a family's slot

- **WHEN** a theme's slot names a dialog's maximum width, a popover's padding, a toast's dot, a bar's
  height or a card's border
- **THEN** the web's lowering writes it and Photon's layout places it, and under `PhotonTheme` neither
  does

#### Scenario: A colour a role gives

- **WHEN** a theme recolours `TextPrimary` over `PhotonTheme`
- **THEN** a Tooltip's fill and a Toast's fill take the new colour, because `PhotonTheme` and
  `MaterialTheme` leave null every slot colour that one of the theme's roles already gives

#### Scenario: Material 3

- **WHEN** a Card is drawn under `MaterialTheme`
- **THEN** its corner is Material 3's medium one, 12dp

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
