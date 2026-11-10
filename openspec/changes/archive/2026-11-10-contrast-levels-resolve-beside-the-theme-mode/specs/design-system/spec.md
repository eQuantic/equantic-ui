## ADDED Requirements

### Requirement: A colour resolves at the system's contrast level

Every colour token SHALL resolve with a `ContrastLevel` beside the `ThemeMode`, and the level SHALL be
read from the system on every target where the system offers one: Increase Contrast on iOS and
macOS, the contrast level on Android 14 and later, and `prefers-contrast: more` in the browser. A
token without a More pair SHALL resolve to its Standard pair. An app SHALL state nothing to get it.

#### Scenario: A fill the theme steps up

- **WHEN** Photon paints PhotonTheme's dark Primary fill with the host at `ContrastLevel.More`
- **THEN** it paints `#AED5FE`, the handoff's More leg, where Standard paints `#76BAFF`

#### Scenario: Muted text in the browser

- **WHEN** the browser reports `prefers-contrast: more` on a page of an app using PhotonTheme
- **THEN** every element painted in Muted text paints `#475261` in light and `#C5D1DF` in dark

#### Scenario: A theme without a More palette

- **WHEN** an app's theme states no More pair for any token
- **THEN** it paints the same at both levels, and its stylesheet writes no contrast block

### Requirement: The palette clears its floors at each contrast level

At `ContrastLevel.More`, PhotonTheme SHALL clear the WCAG 2 targets on the strictest ground (12:1,
9:1 and 7:1 for Primary, Secondary and Muted text, 3:1 and 4.5:1 for Border and BorderStrong, 7:1
for every label on a fill or a Subtle) and the APCA floors one level up (Lc 90 for body text, 75 for
Muted and labels, 30 for BorderStrong). Its More values SHALL be the ones `tokens.json` publishes
under `color.more`, compared both ways.

#### Scenario: A More value drifts

- **WHEN** a More value in `tokens.json` differs from PhotonTheme's
- **THEN** `HandoffTokenPinTests` fails naming the token and both values

#### Scenario: A token steps up without the handoff

- **WHEN** PhotonTheme gives a token a More pair that `color.more` does not list
- **THEN** `HandoffTokenPinTests` fails saying the SDK steps it up and the handoff lists no More pair
