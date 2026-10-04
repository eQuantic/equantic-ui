## ADDED Requirements

### Requirement: A shortcut that is not enabled binds nothing

A `Shortcut` whose `Enabled` is false SHALL stay in the tree and bind nothing: on the web it SHALL
carry no marker and declare no binding, and on Photon it SHALL add no binding to the frame, so the key
goes on to whatever else would take it. A `Shortcut` SHALL be enabled unless it says otherwise.

#### Scenario: A disabled chord inside an enabled one

- **WHEN** a shortcut that is not enabled binds Escape inside one that binds Escape too, and Escape is
  pressed
- **THEN** the outer one answers, and the inner one does not

#### Scenario: A disabled chord alone

- **WHEN** the only shortcut that binds Escape is not enabled, and Escape is pressed
- **THEN** nothing takes the key, and on the web the browser keeps it

### Requirement: The inner of two nested shortcuts of one chord answers

Of two shortcuts of one chord where one wraps the other, both live, the inner one SHALL answer, on
the web and on Photon, as the shortcuts nearest the focus answer first in Flutter. Of two siblings,
the later one SHALL answer, as before.

#### Scenario: A component's own Escape inside a dialog

- **WHEN** a shortcut that binds Escape wraps another that binds Escape, and Escape is pressed
- **THEN** the inner one answers, and the outer one does not

### Requirement: The find bar's Escape is its editor's own

The code editor's Escape SHALL close the find bar of the editor the keyboard is in, the bar or the
code, and SHALL bind nothing while that editor's bar is closed.

#### Scenario: Two editors with their bars open

- **WHEN** two code editors on one page have their find bars open, the keyboard is in the first, and
  Escape is pressed
- **THEN** the first editor's bar closes and the second's stays open

#### Scenario: A dialog around an editor whose bar is closed

- **WHEN** a dialog that closes on Escape holds a code editor whose find bar is closed, the keyboard
  is in the editor, and Escape is pressed
- **THEN** the dialog's Escape answers

#### Scenario: A dialog around an editor whose bar is open

- **WHEN** a dialog that closes on Escape holds a code editor whose find bar is open, the keyboard is
  in the editor, and Escape is pressed
- **THEN** the bar closes and the dialog stays open, and the next Escape closes the dialog

### Requirement: A control's chords stay in the tree while its panel is closed

A `Select`, a `Menu`, a `TimePicker` and a `DatePicker` SHALL keep the chords their panel answers to
around their tree whether the panel is open or not, enabled only while it is, so that opening the
panel moves nothing in the tree and the keyboard focus stays on a node of the frame.

#### Scenario: Opening a Select with the keyboard on Photon

- **WHEN** a `Select`'s field holds the keyboard focus and Enter opens its panel
- **THEN** the focus stays on the field, a node of the frame, where it named a path the field had left
