## ADDED Requirements

### Requirement: The system back closes what is open

The platform's back SHALL run what Escape runs on a desktop: a field, a code surface or a sheet being
edited SHALL stop being edited, and otherwise the topmost layer that binds Escape (a dismissible
`Dialog`, a `BottomSheet`, a `Drawer`, an open `Anchored` menu, a picker) SHALL close through that
binding. A screen with a back of its own SHALL declare it the same way, as a `Shortcut` of
`KeyChord.Escape`.

#### Scenario: A dismissible dialog

- **WHEN** back is pressed with a dismissible `Dialog` on screen
- **THEN** its `onDismiss` runs once and the app stays in front

#### Scenario: Two layers

- **WHEN** back is pressed with a `BottomSheet` over a dismissible `Dialog`
- **THEN** the sheet closes and the dialog does not

#### Scenario: An open drawer

- **WHEN** back is pressed with a `Drawer` open
- **THEN** the drawer closes and the app stays in front

#### Scenario: A field being edited

- **WHEN** back is pressed while a `TextEntry` has the caret and nothing else is open
- **THEN** the field stops being edited, and the next back is the system's

### Requirement: Back is the system's when nothing is open

When there is no edit and no Escape binding on screen, the app SHALL take no back, and the platform's
own back SHALL run, which leaves the app. A focus ring alone SHALL NOT take back, and SHALL stay where
it is. A host SHALL be able to tell before a back arrives whether it would take one
(`PhotonHost.CanGoBack`).

#### Scenario: Nothing open

- **WHEN** back is pressed on a screen with no edit, no dialog, no sheet, no drawer and no menu
- **THEN** the app takes nothing and Android leaves the app

#### Scenario: A dialog that refuses to be dismissed

- **WHEN** back is pressed with a `Dialog` that is not dismissible
- **THEN** the dialog does not close, and back is the system's

#### Scenario: A focus ring

- **WHEN** back is pressed with a button holding the keyboard focus and nothing else open
- **THEN** the app takes nothing and the focus stays on the button

### Requirement: A list-detail goes back on a phone

A `ListDetail` showing its detail in the compact shape, with an `OnBack`, SHALL bind Escape to
`OnBack`, so back leaves the detail for the list. It SHALL bind nothing in the wide shape, where both
panes are on screen, nor with the list on screen, nor without an `OnBack`.

#### Scenario: The detail on a phone

- **WHEN** back is pressed on a 390dp-wide screen showing a `ListDetail`'s detail
- **THEN** its `OnBack` runs once

#### Scenario: Both panes on a tablet

- **WHEN** back is pressed on a 1100dp-wide screen showing a `ListDetail` with an item chosen
- **THEN** its `OnBack` does not run, and back is the system's

### Requirement: Android hears whether the app takes back before the gesture

The Android shell SHALL register an `OnBackInvokedCallback` from Android 13 only while the host can go
back, and unregister it when it cannot, so the system plays its predictive "leave the app" animation
for every back the app does not take. Where the platform delivers back as `KEYCODE_BACK`, the activity
SHALL take the key on the same condition. One press SHALL reach the host once.

#### Scenario: A detail, then the list, on Android 16

- **WHEN** the native template's list-detail app shows a message and back is pressed twice
- **THEN** the first back shows the list with the app in front, and the second leaves the app

#### Scenario: The key path

- **WHEN** the same app opts out of predictive back and back is pressed twice
- **THEN** the results are the same, through `KEYCODE_BACK`
