# Spec Delta

## ADDED Requirements

### Requirement: Undo and redo raise the edit they made

`CodeEditorController.Changed` SHALL carry the edit an undo or a redo made: the range it replaced, the
text it removed and the text it wrote. Null SHALL mean only a document replaced whole.

#### Scenario: Undoing a word

- **WHEN** `ab` is typed into an empty editor and undone
- **THEN** `Changed` carries an edit that removed `ab` from 0:0 to 0:2 and wrote nothing

### Requirement: A code surface draws its notes over its code and caret

A `CodeSurface`'s `Notes` SHALL be drawn at their offsets in the surface's coordinates, over its child
and over its carets, on both targets. While the pointer is over a note the realizer SHALL report
nothing to the surface's model, and a press on a note SHALL NOT move the caret.

#### Scenario: A note over the caret

- **WHEN** a surface carries a note whose box covers the caret
- **THEN** the note is drawn over the caret, and a press on it leaves the selection where it was

### Requirement: The hosts report a pointer passing over a code surface and leaving it

Both realizers SHALL hand a code surface's model `PointerPhase.Hover`, with the pointer's position in
the surface's coordinates, when a pointer moves over the surface with nothing pressed, and
`PointerPhase.Exit` when it leaves the surface.

#### Scenario: A pointer passing over the code

- **WHEN** a mouse moves over a code surface with no button down and then leaves it
- **THEN** the model hears `Hover` at each position and then `Exit`
