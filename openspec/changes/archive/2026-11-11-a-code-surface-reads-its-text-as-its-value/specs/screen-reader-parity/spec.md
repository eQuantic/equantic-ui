## ADDED Requirements

### Requirement: A code surface is read as a field holding its text

A code surface SHALL be read as one field whose value is what its model says a reader hears it hold,
`ICodeSurfaceModel.Reading`, on every target, and the text it draws SHALL NOT be read as lines beside that
field. On the web the field's selection SHALL stand where the model's caret and selection stand. A
spreadsheet's field SHALL hold what the grid says now. Where the page's primary pointer is coarse, the web's
door SHALL hold nothing, because a phone's keyboard edits the text around its caret.

#### Scenario: A code editor's caret moves

- **WHEN** a code editor's caret stands on a line of a long file
- **THEN** the field holds that line and the lines around it, and on the web its selection is the caret

#### Scenario: The name box at rest

- **WHEN** a spreadsheet's name box shows the active cell's address
- **THEN** a reader hears a field holding that address, and no line of text beside it

#### Scenario: An input method composes in a code editor on the web

- **WHEN** an input method's composition is open in the door
- **THEN** nothing moves the door's selection, and after the commit the door holds the reading again

#### Scenario: A code editor under a finger

- **WHEN** a page whose primary pointer is coarse draws a code editor
- **THEN** its door holds no text, and typing reaches the document as it did before
