# code-diagnostics Specification

## Purpose
The problems a code editor shows, the same on the web and on Photon: what its app reports and what
its providers answer as the text changes, how each travels with the edits after it, how they are
asked for without flooding a language server, how they are drawn under the code and in the gutter,
and how the keyboard walks them.

## Requirements

### Requirement: The editor shows the problems reported to it

A code editor's diagnostics (`CodeDiagnostics`) SHALL hold the problems its app reported
(`Report`) and the latest answer of each of its providers, and SHALL show their union, in document
order. A report from the app SHALL replace only what the app reported before, and an answer from a
provider only what that provider answered before. `CodeEditor.Diagnostics` SHALL be reported to the
editor's diagnostics whenever it holds other problems than the ones last reported. `Diagnostics`,
`DiagnosticProviders` and `HoverProviders` SHALL reach the editor a position keeps from its parent's
next build, as its other configuration does.

#### Scenario: The app reports, then a provider answers

- **WHEN** the app reports an error on line 3 and a provider answers a warning on line 1
- **THEN** the diagnostics hold the warning, then the error

#### Scenario: The app reports again

- **WHEN** the app reports an error on line 3, a provider answers a warning on line 1, and the app
  reports nothing
- **THEN** the diagnostics hold the warning alone

#### Scenario: A parent builds again with other problems

- **WHEN** a parent built its editor with an error on line 1, and builds again with an error on line 6
- **THEN** the editor its position keeps holds the error on line 6 alone

### Requirement: Only a change is announced

`CodeDiagnostics.Changed` SHALL be raised when the problems it holds change, and SHALL NOT be raised
by an answer, a report or an edit that leaves them equal to those held. The answer of a provider
taken out of `Providers` SHALL go with it: put back, the provider SHALL show nothing until it answers
again. A provider is known by reference, so one handed anew is asked at once.

#### Scenario: The same answer again

- **WHEN** a provider answers the problems it answered before, and a character is typed after every
  problem
- **THEN** `Changed` is not raised

#### Scenario: A provider made in every build

- **WHEN** a parent builds again whenever the problems change and makes its editor's provider anew in
  every build
- **THEN** the builds stop once the provider has answered the same problems twice

#### Scenario: A provider put back

- **WHEN** a provider that answered a warning is taken out and put back, and nothing asks it again
- **THEN** the diagnostics hold no warning

#### Scenario: Every provider taken away

- **WHEN** a parent builds again with no problem providers, after one found an error
- **THEN** the error is gone at once, with no edit

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

### Requirement: Providers are asked once typing pauses

When the document changes and the diagnostics have providers, they SHALL wait `Delay` after the last
edit and then ask every provider about the document as it stands; an edit during the wait SHALL
restart it. At most one request SHALL be out: an edit SHALL cancel the request's token, and an
answer that arrives after a newer edit SHALL be dropped, whether or not its provider honoured the
token. `Check()` SHALL ask at once. A provider that throws SHALL raise `Failed` with what it threw,
and the other providers' answers SHALL still show.

#### Scenario: A burst of keystrokes

- **WHEN** five characters are typed, each within the delay of the one before
- **THEN** one request is made, about the document with all five characters

#### Scenario: A keystroke while a request is out

- **WHEN** a provider has been asked and has not answered, and a character is typed
- **THEN** the request's token is cancelled, and its answer, when it comes, changes nothing

#### Scenario: A provider that throws

- **WHEN** one provider throws and another answers a warning
- **THEN** `Failed` is raised with the first one's exception and the warning shows

### Requirement: A problem is drawn under its range in its severity

The editor SHALL draw an error, a warning and an information as a wavy line under their range, in the
destructive, the warning and the info colour, and a hint as three dots under its first characters in
the muted ink. A problem whose range is empty SHALL be drawn one cell wide where it stands: under the
character there, or the cell after the line's end, an empty line's included. The gutter SHALL mark each line with a problem as an error or
a warning, the worst problem of the line deciding, behind a marker the app put on that line itself.

#### Scenario: An error and a warning on two lines

- **WHEN** an editor holds an error on line 1 and a warning on line 2
- **THEN** a wave in the destructive colour is drawn under the error's range, a wave in the warning
  colour under the warning's, and the gutter marks line 1 as an error and line 2 as a warning

### Requirement: F8 walks the problems

F8 SHALL put the caret at the start of the first problem after it, and Shift+F8 at the start of the
last problem before it, wrapping at either end of the document, and SHALL show that problem's card.
Both keys SHALL be claimed only while the editor holds a problem.

#### Scenario: Walking forward past the last problem

- **WHEN** an editor holds problems on lines 2 and 5, its caret on line 6, and F8 is pressed twice
- **THEN** the caret is at the start of the problem on line 2, then of the one on line 5

#### Scenario: No problem

- **WHEN** an editor holds no problem and F8 is pressed
- **THEN** the editor does not claim the key
