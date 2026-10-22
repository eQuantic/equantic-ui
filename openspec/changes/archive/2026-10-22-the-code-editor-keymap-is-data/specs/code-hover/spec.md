# Spec Delta

## MODIFIED Requirements

### Requirement: The keyboard shows and closes the card

⌘K followed by ⌘I (Ctrl+K, Ctrl+I) SHALL show the card for the caret at once; ⌘K SHALL be claimed only
while a card could show there (a hover provider, or a problem at the caret, and no completion list), and a
press SHALL end a chord half typed, as SHALL any key but a modifier, whatever takes it.
Escape SHALL close a card and be claimed when one shows, after a completion list showing, which it
closes first, and a completion list asked for and not yet showing SHALL go with the card. A card the
keyboard asked for SHALL close when the caret moves or the text changes. A card asked for over one that
shows SHALL replace it, and SHALL announce that it closed while nothing is
there to show yet. The pointer leaving SHALL end only what the pointer asked for: a card the keyboard
showed stays, and so does a request the keyboard made that its providers have not answered yet.

#### Scenario: A problem on another line

- **WHEN** no hover provider is set, a problem is reported on another line than the caret's, and ⌘K is
  pressed
- **THEN** the editor does not claim the key, which is the host's

#### Scenario: Showing the card at the caret

- **WHEN** the caret is on a word a provider explains and ⌘K then ⌘I are pressed
- **THEN** the card for that word shows without waiting for the delay

#### Scenario: A key the list takes between the halves

- **WHEN** ⌘K is pressed, then ⌃Space opens the completion list and Escape closes it, then ⌘I is pressed
- **THEN** no card shows

#### Scenario: Escape

- **WHEN** a card shows and Escape is pressed
- **THEN** the card closes, the editor claims the key, and Tab still indents

#### Scenario: Escape over a list not yet showing

- **WHEN** a word is typed whose completion provider has not answered, ⌘K ⌘I shows a card, and Escape is
  pressed
- **THEN** the card closes, the list asked for is dropped, its answer opens no list, and Tab still
  indents

#### Scenario: The pointer leaving while the keyboard asks

- **WHEN** ⌘K ⌘I asks a slow provider about the caret and the pointer leaves the surface before it
  answers
- **THEN** the card shows when the provider answers

#### Scenario: The pointer's wait under the keyboard's card

- **WHEN** F8 shows a problem's card, the pointer rests on another word and leaves the surface before
  the delay
- **THEN** the problem's card stays, and no card for the other word follows
