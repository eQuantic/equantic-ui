# Spec Delta

## ADDED Requirements

### Requirement: A pointer at rest over the code shows the card

When a pointer rests over a character of the code for `CodeHoverSession.Delay`, the editor SHALL
show a card explaining the word under it, or the problem under it when the pointer is on a problem
outside any word. A pointer that passes over the code and leaves before the delay SHALL show nothing
and ask nothing. A pointer past the end of a line, in the gutter or under the last line is over no
character.

#### Scenario: A pointer at rest on a problem

- **WHEN** an error `CS0103: The name 'y' does not exist` covers `y` and the pointer rests on `y`
- **THEN** after the delay a card shows the message, its rule and its source

#### Scenario: A pointer passing over

- **WHEN** the pointer moves over the code and leaves the surface before the delay
- **THEN** no card shows and no provider is asked

### Requirement: The card holds the problems, then the providers' answers

The card SHALL list the problems whose range holds the pointer's position, worst first, and then what
the hover providers answered for that position, in the order of `Providers`. It SHALL show as soon as
anything is there to show and grow as later answers arrive; with nothing to show, no card SHALL show.
An answer that arrives after the pointer moved to another word or left SHALL be dropped.

#### Scenario: A problem and a provider's answer

- **WHEN** the pointer rests on a word with a warning and a provider answers a signature for it
- **THEN** the card shows the warning, then the signature

#### Scenario: A late answer

- **WHEN** a provider is asked about one word and the pointer rests on another before it answers
- **THEN** its answer is not shown

### Requirement: The card stays while what it explains is pointed at

A card the pointer showed SHALL stay while the pointer is in the range it explains or over the card
itself, and SHALL close when the pointer rests outside both or leaves the surface.

#### Scenario: Moving inside the word

- **WHEN** a card shows for `Column` and the pointer moves from its first letter to its last
- **THEN** the card stays and no provider is asked again

#### Scenario: Leaving the surface

- **WHEN** a card shows and the pointer leaves the surface
- **THEN** the card closes

### Requirement: The keyboard shows and closes the card

⌘K followed by ⌘I (Ctrl+K, Ctrl+I) SHALL show the card for the caret at once. Escape SHALL close a
card and be claimed when one shows, after a completion list showing, which it closes first. A card
the keyboard asked for SHALL close when the caret moves or the text changes.

#### Scenario: Showing the card at the caret

- **WHEN** the caret is on a word a provider explains and ⌘K then ⌘I are pressed
- **THEN** the card for that word shows without waiting for the delay

#### Scenario: Escape

- **WHEN** a card shows and Escape is pressed
- **THEN** the card closes, the editor claims the key, and Tab still indents

### Requirement: The card stands above what it explains

The card SHALL stand above the first line of the range it explains, its left edge at the range's
start, and below the line when the editor's viewport has no room above it. Its right edge SHALL stay
inside the viewport, and its left edge too when the code is scrolled sideways. It SHALL be as wide as
its longest line, up to 80 columns and the viewport, and wrap what is longer.

#### Scenario: A word on the first visible line

- **WHEN** a card shows for a word on the first line the editor shows
- **THEN** the card's top is the bottom of that line

### Requirement: A screen reader hears the card the keyboard asked for

A card shown by F8, Shift+F8 or ⌘K ⌘I SHALL be a polite live region on both targets; a card the
pointer showed SHALL NOT be.

#### Scenario: F8

- **WHEN** F8 shows a problem's card
- **THEN** the card is announced as a polite live region
