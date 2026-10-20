# code-hover Specification

## Purpose
The card that explains the code under the pointer or the caret, the same on the web and on Photon:
when it shows, what it holds, where it stands over the code, how it closes, and how the keyboard asks
for it and hears it.

## Requirements

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

The card SHALL list the problems at the position it explains, worst first, and then what the hover
providers answered for that position, in the order of `Providers`. A problem is at the pointer when its
range covers the character under it, a range ending before the character at its end as a word's does,
and at the caret when its range holds the caret, its ends included. The card SHALL show as soon as
anything is there to show and grow as later answers arrive; with nothing to show, no card SHALL show.
An answer that arrives after the pointer moved to another word or left SHALL be dropped. When the
problems change while a card shows or its providers are asked, the card SHALL hold the problems at its
position as they are now, without asking its providers again, and SHALL close when nothing is left to
show; a pointer's wait that has not ended SHALL read them when it ends.

#### Scenario: A problem and a provider's answer

- **WHEN** the pointer rests on a word with a warning and a provider answers a signature for it
- **THEN** the card shows the warning, then the signature

#### Scenario: A late answer

- **WHEN** a provider is asked about one word and the pointer rests on another before it answers
- **THEN** its answer is not shown

#### Scenario: A problem reported after the card showed

- **WHEN** a card shows a provider's answer for a word, and a problem is then reported under it
- **THEN** the card shows the problem and the answer, and no provider was asked again

#### Scenario: The problem a card showed alone, taken away

- **WHEN** F8 shows a card holding one problem, and the problem is then taken away
- **THEN** the card closes

#### Scenario: A problem ending where the pointer rests

- **WHEN** a problem covers `name` in `name = 1`, no provider is set, and the pointer rests on the space
  after it
- **THEN** no card shows, and ⌘K ⌘I with the caret there shows the problem

### Requirement: The card stays while what it explains is pointed at

A card the pointer showed SHALL stay while the pointer is on a character of the range it explains or
over the card itself, and SHALL close when the pointer rests outside both or leaves the surface. A
request the pointer made that is still waiting or unanswered SHALL end the same way.

#### Scenario: Moving inside the word

- **WHEN** a card shows for `Column` and the pointer moves from its first letter to its last
- **THEN** the card stays and no provider is asked again

#### Scenario: Leaving the surface

- **WHEN** a card shows and the pointer leaves the surface
- **THEN** the card closes

#### Scenario: Moving onto the space after the word

- **WHEN** a card shows for `alpha` in `alpha beta`, or the pointer's wait for it has not ended, and the
  pointer moves onto the space after it
- **THEN** the card closes, or no card follows

### Requirement: The keyboard shows and closes the card

⌘K followed by ⌘I (Ctrl+K, Ctrl+I) SHALL show the card for the caret at once; ⌘K SHALL be claimed only
while a card could show (a hover provider, or a problem), and a press SHALL end a chord half typed, as
SHALL any key but a modifier, whatever takes it.
Escape SHALL close a card and be claimed when one shows, after a completion list showing, which it
closes first. A card the keyboard asked for SHALL close when the caret moves or the text changes. A
card asked for over one that shows SHALL replace it, and SHALL announce that it closed while nothing is
there to show yet. The pointer leaving SHALL end only what the pointer asked for: a card the keyboard
showed stays, and so does a request the keyboard made that its providers have not answered yet.

#### Scenario: Showing the card at the caret

- **WHEN** the caret is on a word a provider explains and ⌘K then ⌘I are pressed
- **THEN** the card for that word shows without waiting for the delay

#### Scenario: A key the list takes between the halves

- **WHEN** ⌘K is pressed, then ⌃Space opens the completion list and Escape closes it, then ⌘I is pressed
- **THEN** no card shows

#### Scenario: Escape

- **WHEN** a card shows and Escape is pressed
- **THEN** the card closes, the editor claims the key, and Tab still indents

#### Scenario: The pointer leaving while the keyboard asks

- **WHEN** ⌘K ⌘I asks a slow provider about the caret and the pointer leaves the surface before it
  answers
- **THEN** the card shows when the provider answers

#### Scenario: The pointer's wait under the keyboard's card

- **WHEN** F8 shows a problem's card, the pointer rests on another word and leaves the surface before
  the delay
- **THEN** the problem's card stays, and no card for the other word follows

### Requirement: The card stands above what it explains

The card SHALL stand above the first line of the range it explains, its left edge at the range's
start, and below the line when the editor's viewport has no room above it. Its right edge SHALL stay
inside the viewport, and its left edge too when the code is scrolled sideways, including a slide made
after the card showed (a caret revealed, a wheel). It SHALL be as wide as its longest line, up to 80
columns and the viewport, and wrap what is longer, and its bottom SHALL be the top of the line it
stands above: it is measured in the heights its faces lay out at.

#### Scenario: A word on the first visible line

- **WHEN** a card shows for a word on the first line the editor shows
- **THEN** the card's top is the bottom of that line

#### Scenario: A word in the middle of the view

- **WHEN** a card shows for a word on the eleventh line of a view twenty lines tall
- **THEN** the card's bottom is the top of that line, and its left edge is the word's

#### Scenario: A message longer than the view is wide

- **WHEN** a card holds a message longer than a narrow view is wide
- **THEN** the message wraps inside the card on both targets

#### Scenario: F8 past the right edge

- **WHEN** F8 goes to a problem past the view's right edge, and the code slides to the caret
- **THEN** the card's right edge and its left edge are inside the view

### Requirement: The card is what the pointer is over

A press or a pointer over the card SHALL be the card's, also where a control drawn inside the code
lies under it, and SHALL move no caret, while a control drawn inside the card SHALL keep its own press.
A pointer over a control drawn inside the code is over no character of the code, and a pointer on a
control the card draws is still over the card.

#### Scenario: A press on the card over a button in the code

- **WHEN** a card stands over a button drawn inside the code and is pressed where the button lies under
  it
- **THEN** the button is not pressed, and the card stays

#### Scenario: The pointer on the card's own control

- **WHEN** a card the pointer showed is open and the pointer moves from the card onto a control the card
  draws
- **THEN** the card stays

#### Scenario: The pointer reaching a button in the code

- **WHEN** a card the pointer showed is open and the pointer moves onto a button drawn inside the code,
  outside the card
- **THEN** the card closes

### Requirement: One thing at a time over the caret

The completion list opening SHALL close the card, which SHALL NOT come back when the list closes, and
while a list shows no card SHALL be drawn.

#### Scenario: ⌃Space over a card

- **WHEN** a card shows, ⌃Space opens the completion list and Escape closes it
- **THEN** the card closed when the list opened, and stays closed

### Requirement: A screen reader hears the card the keyboard asked for

A card shown by F8, Shift+F8 or ⌘K ⌘I SHALL be a polite live region on both targets; a card the
pointer showed SHALL NOT be.

#### Scenario: F8

- **WHEN** F8 shows a problem's card
- **THEN** the card is announced as a polite live region
