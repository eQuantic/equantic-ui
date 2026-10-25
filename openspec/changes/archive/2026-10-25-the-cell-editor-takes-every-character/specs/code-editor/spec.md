# Spec Delta

## ADDED Requirements

### Requirement: A measured grid places text by its advance

The code engine SHALL place a caret, a selection band, a press and the column a vertical move aims at
by the measured advance of the line's text when its grid carries a measurer, and by its monospaced pitch
when it does not. Measured, a caret before a column SHALL stand where the text before it ends, a band
SHALL run from where its first character begins to where its last one ends, a press SHALL land on the
nearer side of the text element under it and never inside one, and ↑ and ↓ SHALL aim at the same
distance from the line's start rather than the same count of characters. A grid with no measurer SHALL
place everything exactly as before.

#### Scenario: A caret in a proportional face

- **WHEN** an editor whose grid measures `iii` as 9 points and `iiiW` as 21 holds the line `iiiWi`
  and its caret is placed after `W`
- **THEN** the caret stands 21 points from the line's start

#### Scenario: A press between two characters of different widths

- **WHEN** the same line is pressed 16 points from its start, nearer the end of `W` than its beginning
- **THEN** the caret stands after `W`

#### Scenario: A vertical move by distance

- **WHEN** a measured editor holds `WWWW` over `iiiiiiiiii`, where four `i` are as wide as one `W`,
  and ↓ is pressed with the caret after the first `W`
- **THEN** the caret stands after the fourth `i` of the second line

#### Scenario: The monospaced editor is unchanged

- **WHEN** a code editor whose grid carries no measurer holds a line with a tab, a wide character and
  an emoji
- **THEN** its carets, bands and presses land on the cells they landed on before this change
