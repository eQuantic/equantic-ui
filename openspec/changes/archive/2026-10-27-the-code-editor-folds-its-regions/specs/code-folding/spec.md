## ADDED Requirements

### Requirement: The regions

`CodeEditorController.Folding` SHALL ask every `ICodeFoldProvider` in its `Providers` once the text has rested for
its delay, and the indentation's provider when there is none. The answers SHALL be joined, ordered by their start
line, one region kept for each start line (the one reaching furthest), a region that crosses another dropped,
and a region of no line under its header dropped. An answer that comes after the text changed again SHALL be
dropped, and so SHALL an answer in which a provider failed, the regions and what is folded staying as they were.
An editor whose grid brings rows of its own (a diff's side), or whose folding is turned off
(`CodeFolding.Enabled`, which a spreadsheet's cell entry turns off), SHALL hide nothing, and its fold commands SHALL
claim no key.

#### Scenario: The indentation's regions

- **WHEN** a document reads `class A`, `{`, `    void B()`, `    {`, `        x();`, `    }`, `}` and no provider is given
- **THEN** the regions start on lines 1 and 3, counted from 0, at each `{` the lines under it are indented from,
  the first ending on line 5 and the second on line 4

#### Scenario: Two regions on one line

- **WHEN** a provider answers lines 2 to 5 and lines 2 to 8
- **THEN** one region starts on line 2, and it ends on line 8

### Requirement: Folding and unfolding

`Fold` SHALL fold the innermost region holding the caret's line that is open, `Unfold` SHALL unfold the innermost
folded one, `FoldRecursively` and `UnfoldRecursively` SHALL do the same to it and every region inside it,
`ToggleFold` SHALL fold or unfold it, `FoldAll` SHALL fold every region and `UnfoldAll` SHALL unfold them all. A
folded region SHALL keep its header line and hide the lines under it, and the caret SHALL step over them.

#### Scenario: Fold the method the caret is in

- **WHEN** the caret stands on line 4 of the document above and ⌥⌘[ is pressed
- **THEN** line 4 is hidden, lines 3 and 5 show, ↓ from line 3 goes to line 5, and ⌥⌘] shows line 4 again

#### Scenario: Fold all

- **WHEN** ⌘K ⌘0 is pressed
- **THEN** both regions are folded, lines 2 to 5 are hidden, and ⌘K ⌘J shows them all

### Requirement: The gutter and the marker

The gutter SHALL show a chevron beside every line that starts a region, pointing down while it is open and right
while it is folded, and a press on it SHALL toggle the region. It SHALL be a button for assistive technology,
named by the lines it folds and saying whether it is expanded. A folded header SHALL show `⋯` after its text, and
a press on it SHALL unfold the region, line numbers shown or not. A press on either SHALL leave the keyboard with
the code.

#### Scenario: A press on the chevron

- **WHEN** the chevron beside line 1 is pressed
- **THEN** lines 2 to 5 are hidden, the chevron points right and says it is collapsed, and line 1 shows `⋯`
- **AND** the keys of the editor still reach the code

### Requirement: A fold never hides what is worked on

The regions and the folded ones SHALL be carried through every edit until the next answer, a folded region
whose start line is kept staying folded when it arrives unless it would hide a line a selection ends on. An edit
that changes a hidden line SHALL unfold the regions hiding it, a region whose header line is removed or joined to
the line above SHALL go, and a selection that moves into a hidden line SHALL unfold the regions hiding it. A
command that folds a region over a caret SHALL put that caret at the end of the region's header. A copy or a cut
across a folded region SHALL take its hidden lines.

#### Scenario: Going to a hidden line

- **WHEN** the region starting on line 1 is folded and the caret is put on line 4
- **THEN** the region unfolds and line 4 shows

#### Scenario: A line inserted above

- **WHEN** the region starting on line 1 is folded and a line is inserted above line 0
- **THEN** the region starts on line 2 and lines 3 to 6 are hidden

#### Scenario: Folding over the caret

- **WHEN** the caret stands on line 4 and ⌥⌘[ folds the region starting on line 3
- **THEN** the caret stands at the end of line 3

#### Scenario: An answer that would hide the caret

- **WHEN** a region is folded and the next answer gives its start line a region reaching the caret's line
- **THEN** that region arrives open

#### Scenario: Copying across a fold

- **WHEN** the region starting on line 1 is folded, lines 1 to 6 are selected and copied
- **THEN** the text copied holds lines 1 to 6, the hidden ones among them

### Requirement: The keys of folding

The defaults SHALL bind `Fold` and `Unfold` to ⌥⌘[ and ⌥⌘] on Apple and Ctrl+Shift+[ and Ctrl+Shift+] elsewhere,
`FoldRecursively` and `UnfoldRecursively` to ⌘K ⌘[ and ⌘K ⌘], `ToggleFold` to ⌘K ⌘L, `FoldAll` to ⌘K ⌘0 and
`UnfoldAll` to ⌘K ⌘J, Ctrl standing for ⌘ elsewhere. With no region to act on they SHALL claim nothing.

#### Scenario: No region at the caret

- **WHEN** the caret stands on line 0 of the document above and ⌥⌘[ is pressed
- **THEN** the key is not claimed
