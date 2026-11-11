## ADDED Requirements

### Requirement: A theme's code palette reaches the browser

The shell's theme data SHALL carry the colour of every `CodeTokenKind`, and the browser's theme SHALL answer `Code` from
it, so a theme that overrides `Code` colours a `CodeBlock` alike on the server, on Photon and in the browser.

#### Scenario: A theme that overrides Code

- **WHEN** a theme answers its own colour for `CodeTokenKind.Keyword`
- **THEN** the theme data carries that colour, and the materialized theme answers it for `keyword`
