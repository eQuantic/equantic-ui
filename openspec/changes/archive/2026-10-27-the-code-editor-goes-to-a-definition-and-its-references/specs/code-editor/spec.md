## ADDED Requirements

### Requirement: The hosts report the modifiers a pointer passes with

Every host SHALL hand a code surface the modifiers held with each pointer event, read as its keys are read
(⌘ is Command and ⌃ is Control on a Mac, Ctrl is Command elsewhere), and SHALL hand it a hover at the pointer's
last place whenever the held modifiers change while the pointer rests over it. A press SHALL carry ⇧, ⌥, ⌃
and ⌘ alike.

#### Scenario: ⌘ pressed with the pointer at rest

- **WHEN** the pointer rests over a code surface in a Photon window on macOS and ⌘ is pressed
- **THEN** the surface hears a hover at the same place, with Command

#### Scenario: ⌥ and a press in a macOS window

- **WHEN** ⌥ is held and the code is pressed in a Photon window on macOS
- **THEN** a caret is added where it was pressed, as on the web

### Requirement: A code surface says what the pointer looks like over it

`ICodeSurfaceModel.CursorAt` SHALL answer the pointer's look at a position in the surface's coordinates, and
both realizers SHALL show it as the pointer moves over the surface: the text cursor over the code, and the hand
over a link.

#### Scenario: The hand over a link

- **WHEN** ⌘ is held over a word whose definition a provider answers
- **THEN** `CursorAt` answers the hand there, the web surface's cursor is `pointer`, and Photon shows the
  pointing hand
