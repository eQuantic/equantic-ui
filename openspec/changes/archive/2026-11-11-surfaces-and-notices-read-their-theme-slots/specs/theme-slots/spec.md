## ADDED Requirements

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
