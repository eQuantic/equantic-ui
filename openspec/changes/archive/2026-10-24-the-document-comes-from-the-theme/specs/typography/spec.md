# Spec Delta

## ADDED Requirements

### Requirement: Text with no face of its own is set in the theme's text face

A text whose role and call site name no family SHALL be set in the theme's `TextFamily` when it is proportional, and in `MonoFamily` when it is monospaced. A family the role or the call site named SHALL stay. Photon and the email SHALL resolve it through the one merge of a role, a node and the theme. The web SHALL reach it through the document's face, which every text inherits.

#### Scenario: A brand's text face

- **WHEN** a theme answers `TextFamily` "Atkinson Hyperlegible Next", and a `Text` in a role with no family is laid out
- **THEN** Photon measures and paints it in that face, and the web sets the document's `font-family` to it

#### Scenario: A role's own face stays

- **WHEN** the same theme cuts its Heading role in "Newsreader"
- **THEN** a heading is set in "Newsreader" on every target

#### Scenario: Code keeps the code face

- **WHEN** a monospaced `Text` is laid out under a theme that answers both `TextFamily` and `MonoFamily`
- **THEN** it is set in `MonoFamily`
