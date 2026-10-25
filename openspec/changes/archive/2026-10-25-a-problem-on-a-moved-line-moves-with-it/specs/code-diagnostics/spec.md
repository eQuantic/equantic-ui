## MODIFIED Requirements

### Requirement: A problem travels with the edits after it

Every edit SHALL carry each problem's range through it the moment the document takes it, before any
listener hears the change: a position before the edit SHALL stay; one after it SHALL shift by what the
edit removed and inserted; a range's start inside the replaced text SHALL go to the start of the
replacement and its end to the end of the replacement, so a problem whose text was rewritten covers the
rewrite. A problem SHALL NOT grow at its edges: text inserted where it starts or where it ends is not in
it. Undo and redo SHALL carry problems as any edit does. A problem reported at any moment SHALL describe
the text as it stands then, and SHALL NOT be carried through an edit that text already holds. A document
replaced whole SHALL drop every problem.

#### Scenario: Typing before a problem

- **WHEN** `int x = y;` holds an error over `y` and `long ` is typed at the start of the line
- **THEN** the error covers `y` in `long int x = y;`

#### Scenario: Typing right after a problem

- **WHEN** an error covers `y` in `int x = y;` and `z` is typed right after `y`
- **THEN** the error covers `y` alone

#### Scenario: Rewriting the problem's text

- **WHEN** an error covers `y` and the `y` is selected and replaced by `count`
- **THEN** the error covers `count`

#### Scenario: Undo

- **WHEN** an error covers `y`, a line is inserted above it, and the insertion is undone
- **THEN** the error covers `y` on its first line again

#### Scenario: A problem reported after a listener's edit

- **WHEN** a listener inserts a line at the document's start as it hears the second of three lines moved
  up, and then it, or a listener after it, reports a problem over the moved line
- **THEN** the problem stands where it was reported

#### Scenario: Another file opened

- **WHEN** an editor holding problems is given a new document
- **THEN** it holds none
