# Spec Delta

## ADDED Requirements

### Requirement: A list asks once when a word starts, and filters after that

An editor's completion SHALL ask its providers when a word starts (a word character typed where no
list is open, a provider's trigger character, ⌃Space), and SHALL filter and rank what they answered
on every keystroke after that without asking again, unless a provider called its answer incomplete,
which SHALL be asked again whenever the word changes.

#### Scenario: A word typed letter by letter

- **WHEN** a provider offers `Column`, `ColorToken`, `Row` and `Collection` and `Col` is typed, then `u`
- **THEN** the provider was asked once, with the trigger `Typing` and the character `C`, the list
  showed `Collection`, `ColorToken`, `Column`, and then `Column` alone

#### Scenario: An incomplete answer

- **WHEN** one provider answers incomplete and another complete, and `Co` is typed
- **THEN** the first is asked again with the trigger `Incomplete`, and the second is not

### Requirement: A trigger character asks its provider alone, from after it

A character a provider declares in `TriggerCharacters` SHALL open a list that asks that provider only,
for the word that starts after the character.

#### Scenario: A dot after a name

- **WHEN** a provider declaring `.` offers `Theme` and `Route`, another declaring nothing is also
  given, and `context.` is typed
- **THEN** only the first is asked for the dot, at the column after it, and the list shows `Route`,
  `Theme`

### Requirement: No list opens unasked where a word is prose

A word started in a comment or a string, or a number, SHALL open no list, and ⌃Space SHALL open one
anywhere.

#### Scenario: A comment, then ⌃Space

- **WHEN** `// Co` is typed in a C# editor, then ⌃Space is pressed
- **THEN** no provider was asked for the typing, and the list opens on ⌃Space

### Requirement: The list's keys are its own while it shows

While a list shows, ↓ and ↑ SHALL move its selection round from the last entry to the first, the page
keys SHALL move it a page and stop at either end, Tab SHALL accept the selected entry, Enter SHALL
accept it only when accepting changes the text, and Escape SHALL close the list and nothing else. With
one entry showing, an arrow SHALL move the caret.

#### Scenario: A word typed out in full

- **WHEN** `int` is typed and the list shows `int` and `interface`, and Enter is pressed
- **THEN** the line ends, and the list closes

#### Scenario: Escape twice

- **WHEN** a list shows and Escape is pressed twice
- **THEN** the first closes the list and is claimed, and only the second releases the trap on Tab

### Requirement: Accepting writes over the word typed, as one step

Accepting SHALL replace the word typed since the list opened, or the provider's own range with its end
moved by what was typed after the provider answered, leave the caret after the text, and be one undo
step.

#### Scenario: Roslyn's answer after a dot

- **WHEN** the playground's recorded Roslyn answer for `context.` is the provider, `Th` is typed and
  Enter pressed
- **THEN** the line reads `Text(context.Theme);` with the caret after `Theme`

#### Scenario: Undoing an accepted entry

- **WHEN** `Co` is typed, `Column` accepted, and the edit undone
- **THEN** the text reads `Co` again

### Requirement: An answer nobody waits for is dropped

An answer SHALL be applied only while its request is the newest and its list is open; an older one
SHALL be dropped however late it arrives, and its request SHALL be cancelled.

#### Scenario: A list closed before its answer

- **WHEN** a word is started, Escape pressed, and then the provider answers
- **THEN** no list opens, and the request's token was cancelled

#### Scenario: Two requests answered out of order

- **WHEN** `a` and then `.` start two requests, and the second is answered before the first
- **THEN** the list shows the second's answer only

### Requirement: The list closes when the word is left

The list SHALL close when the caret leaves the line or goes before the word's start, when a
selection is made, when a character that ends a word is typed, when the editor loses the keyboard,
and when its document is replaced. A word typed away to nothing SHALL close it unless the list opened
with nothing typed.

#### Scenario: Backspace to the start

- **WHEN** `Co` is typed and Backspace pressed twice, and, in another editor, `context.Th` is typed and
  Backspace pressed twice
- **THEN** the first list is closed and the second still shows `Theme`

### Requirement: An editor offers nothing until it is given a provider

An editor's completion SHALL start with no provider, and SHALL open no list and claim no key until one
is added.

#### Scenario: A new editor

- **WHEN** `ret` is typed and ⌃Space pressed in a new editor
- **THEN** no list opens and ⌃Space is not claimed

### Requirement: The filter matches parts in order

The filter SHALL match a pattern's characters in order, case aside, the first where a part of the
word starts, and SHALL score the best way to lay one over the other by the rule its documentation
states, a run of characters above the same characters apart.

#### Scenario: Parts and runs

- **WHEN** `col` is matched against `Column` and `CopyLine`, `lum` against `Column`, and `cou` against
  `ConsoleOutput`
- **THEN** `Column` scores above `CopyLine`, `lum` matches nothing, and `cou` marks the `C`, the `O`
  and the `u` of `ConsoleOutput`

### Requirement: An editor with no language service still completes

The built-in providers SHALL offer the language's own words (`ICodeLanguage.Keywords`) wherever a word
starts and never right after a dot, and the document's words, each once, leaving out the word being
typed and numbers.

#### Scenario: A C# editor with both

- **WHEN** both are given to a C# editor holding `result = compute();` and `re` is typed on a new line
- **THEN** the list holds `readonly`, `record`, `ref`, `required`, `result` and `return`

### Requirement: The web completes as .NET does

The engine's twin SHALL score, rank, select and accept exactly as the engine does.

#### Scenario: Seeded sessions over a real file

- **WHEN** 60 seeded sessions of keystrokes run over a real source file on .NET and in the embedded Bun
- **THEN** after every keystroke both show the same list, the same selection, the same line and the
  same caret
