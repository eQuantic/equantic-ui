## MODIFIED Requirements

### Requirement: The arrows walk the overloads

With more than one signature, ↑ and ↓ SHALL show the previous and the next signature, round from the last
to the first, while no completion list of more than one entry shows and no hover card hides the panel, as
VS Code's keybindings have it, and ⌥↑ and ⌥↓ (Alt+↑, Alt+↓) SHALL do so beside a list too; with one, or
under a card, ↑ and ↓ SHALL move the caret and ⌥↑ and ⌥↓ the lines (`code-lines`). A later answer that
holds as many signatures SHALL keep the signature chosen.

#### Scenario: Three overloads

- **WHEN** the panel shows three signatures, the first active, and ↓ is pressed twice and then ↑ three
  times
- **THEN** the active signature is the second, the third, the second, the first and the third

#### Scenario: Beside a list

- **WHEN** the panel shows three signatures, a word typed opens the completion list, and ⌥↓ is pressed
- **THEN** the second signature shows and the list keeps its selection

#### Scenario: Beside a list of one entry

- **WHEN** the panel shows three signatures, a word typed opens a list of one entry, and ↓ is pressed
- **THEN** the second signature shows and the list stays open

#### Scenario: A card over the panel

- **WHEN** the panel shows three signatures, ⌘K ⌘I shows a card, which hides it, and ↑ is pressed
- **THEN** the caret moves up a line, and no other signature was chosen
