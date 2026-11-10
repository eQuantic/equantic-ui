# Tasks

## 1. Measured on main

- [ ] 1.1 Every `Hover` a component states on a box inside a `Pressable`, and whether that box is the control's whole surface. Verify: a list in the proposal's evidence, each box's bounds against its control's

## 2. A hover is its control's

- [ ] 2.1 The web's two producers write the control's hover family for a box inside a control, and leave out a box of a nested control. Verify: a test pins the selector, and Chromium draws the arrow nudged with the pointer over the label
- [ ] 2.2 Photon lends the tracked hover to the control's subtree, up to a nested control. Verify: a host test with the pointer over the label draws the arrow nudged

## 3. A foreground descends

- [ ] 3.1 `BoxStyle.Foreground` and `StyleDiff.Foreground`, and the text and icons that name no colour take it on both targets. Verify: the scenarios on both targets, the parity fixture, and the transpiled pins regenerated
- [ ] 3.2 The pressable's button and the link inherit their colour. Verify: a text in a button in dark mode draws `TextPrimary`'s dark value in Chromium

## 4. A fractional translation

- [ ] 4.1 `Transform2D.FractionalTranslate` on both targets. Verify: the band scenario on both

## 5. The handoff's primary button, and the documentation

- [ ] 5.1 The primary button's sheen band, arrow and label. Verify: in Chromium and on Photon
- [ ] 5.2 The wiki's Styling page in its five languages, the `WidgetStateProperty` row of `docs/FLUTTER-PARITY.md`, and one ledger entry citing #614, with the migration line
