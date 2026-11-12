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

#### Scenario: Material 3

- **WHEN** a Card is drawn under `MaterialTheme`
- **THEN** its corner is Material 3's medium one, 12dp
