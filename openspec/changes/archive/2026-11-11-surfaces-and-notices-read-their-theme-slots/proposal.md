# Proposal

Closes #1243 (E6c), a sub-issue of #938 (the theme slots).

## Why

The third slice of the theme slots, on E6a's mechanism. The Dialog, the BottomSheet, the Drawer, the
menus, the notices, the progress indicators and the Card wrote their look as constants (the dialog's
maximum width of 480 and its paddings, elevations, shapes, colours), so a design system could not say
any of it.

## What Changes

- Five slots on `IAppTheme`, each a record with every member optional: `Modal` (Dialog, BottomSheet,
  Drawer), `Menu` (Popover, Menu, Tooltip), `Notice` (Toast, Banner, CookieConsent), `Progress`
  (ProgressBar, PullToRefresh, Skeleton) and `Card`. Each component reads its slot in the order E6a
  set, and what is behaviour (a pull's threshold, a duration) stays the component's.
- `theme.With(…)` takes the five, and they cross to the browser with the rest of the theme, where the
  runtime mirrors and builds each.
- `PhotonTheme` fills them with the sizes its components always had, so its pinned outputs stay as
  they were, and `MaterialTheme` with Material 3's component specifications: a card's medium corner and
  no outline on an elevated one, a linear progress track.
- The surfaces' slot is `Modal`, not `Surface`: `IAppTheme.Surface` is already a colour, and the theme's
  JSON already writes the surface colours under `surfaces`.

The parts reached are the vocabulary (`eQuantic.UI.Primitives`), the components, `MaterialTheme`, the
theme bridge's JSON and the runtime's mirrors. The public surface grows by the five records, the five
members of `IAppTheme` and `With`'s five parameters. No member an app calls goes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `theme-slots`: the surfaces, menus, notices, progress and cards read their slots.
