# Tasks

## 1. Measured on main

- [x] 1.1 Every `Hover` a component states on a box inside a `Pressable`, and whether that box is the control's whole surface. Verify: a list in the proposal's evidence, each box against its control. Measured: every one is the control's own surface, the box the `Pressable` wraps (Button, Chip, IconButton, ListItem, Pagination, Calendar, Accordion, Menu, Select's field and options, TimePicker's field and slots, SearchField's clear button, DataTable's rows, the code completion and go-to lists, the dashboard's search box); the rest are outside every control (Table's rows, the dashboard's navigation links, the map's places, the charts' fragment links). None hovers a smaller box inside its control, so no hover a component states changes where it applies

## 2. A hover is its control's

- [x] 2.1 The web's two producers write the control's hover family for a box inside a control, and leave out a box of a nested control. Verify: a test pins the selector, and Chromium draws the arrow nudged with the pointer over the label
- [x] 2.2 Photon lends the tracked hover to the control's subtree, up to a nested control. Verify: a host test with the pointer over the label draws the arrow nudged

## 3. A foreground descends

- [x] 3.1 `BoxStyle.Foreground` and `StyleDiff.Foreground`, and the text and icons that name no colour take it on both targets. Verify: the scenarios on both targets, the parity fixture, and the transpiled pins regenerated
- [x] 3.2 The pressable's button and the link inherit their colour. Verify: in Chromium, a label with no colour inside the control's button draws its surface's foreground in both themes, the button writing `color: inherit`

## 4. A fractional translation

- [x] 4.1 `Transform2D.FractionalTranslate` on both targets. Verify: the band scenario on both

## 5. The handoff's primary button, and the documentation

- [x] 5.1 The equantic.tech primary button (the site's, built in its own repository) composed from the three: its band, arrow and label. Verify: in Chromium, both themes, rest and hover, with the label's contrast against the surface; on Photon, the arrow, the foreground and the fraction each have their host test
- [ ] 5.2 The wiki's Styling page in its five languages, the `WidgetStateProperty` row of `docs/FLUTTER-PARITY.md`, and one ledger entry citing #614, with the migration line
