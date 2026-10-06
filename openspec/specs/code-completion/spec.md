# code-completion Specification

## Purpose
The completion of a code editor, written once in the code engine: when it asks its providers, how
it filters and ranks what they answered, what its keys do and what accepting writes, the same on .NET
and on the web.

## Requirements

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
one entry showing, an arrow SHALL move the caret. Every key but Tab and Escape, the list's own and
⌃Space among them, SHALL arm the trap on Tab again.

#### Scenario: A word typed out in full

- **WHEN** `int` is typed and the list shows `int` and `interface`, and Enter is pressed
- **THEN** the line ends, and the list closes

#### Scenario: Escape twice

- **WHEN** a list shows and Escape is pressed twice
- **THEN** the first closes the list and is claimed, and only the second releases the trap on Tab

#### Scenario: A list asked for after Escape

- **WHEN** Escape is pressed with no list, ⌃Space opens one, Escape closes it, and Tab is pressed
- **THEN** Tab indents, since a key was pressed after the Escape that released the trap

### Requirement: Accepting writes over the word typed, as one step

Accepting SHALL replace the word typed since the list opened, or the provider's own range, whose end
keeps its distance from the end of the line (what is typed or deleted at the caret moves it, and a move
of the caret does not), leave the caret after the text, and be one undo step.

#### Scenario: Roslyn's answer after a dot

- **WHEN** the playground's recorded Roslyn answer for `context.` is the provider, `Th` is typed and
  Enter pressed
- **THEN** the line reads `Text(context.Theme);` with the caret after `Theme`

#### Scenario: Undoing an accepted entry

- **WHEN** `Co` is typed, `Column` accepted, and the edit undone
- **THEN** the text reads `Co` again

#### Scenario: A provider's range after an arrow

- **WHEN** a provider asked in `Fo|bar()` replaces the whole word with `FooBaz`, the caret moves left
  one character, and Enter is pressed
- **THEN** the line reads `FooBaz()`

### Requirement: A commit character accepts first

A character the selected entry commits on SHALL accept it and then be typed after it. An input
method's composition SHALL come out of the document before the entry is accepted.

#### Scenario: A dot after a class

- **WHEN** `Con` is typed, the list selects `Console`, which commits on `.`, and `.` is typed
- **THEN** the text reads `Console.`

#### Scenario: A dot an input method commits

- **WHEN** `Con` is typed, an input method composes `s` after it and commits `.`
- **THEN** the text reads `Console.`

### Requirement: One entry is listed once

Offers of one label and one inserted text SHALL be listed once, as the first provider's copy that
matches the word, so that a copy that does not match hides none that does.

#### Scenario: Two providers offering Column

- **WHEN** the first provider's `Column` filters by `zzz`, the second's by its label, and `Col` is typed
- **THEN** `Column` is listed once, the second provider's

### Requirement: An answer nobody waits for is dropped

An answer SHALL be applied only while its request is the newest and its list is open; an older one
SHALL be dropped however late it arrives, and its request SHALL be cancelled.

#### Scenario: A list closed before its answer

- **WHEN** a word is started, Escape pressed, and then the provider answers
- **THEN** no list opens, and the request's token was cancelled

#### Scenario: Two requests answered out of order

- **WHEN** `a` and then `.` start two requests, and the second is answered before the first
- **THEN** the list shows the second's answer only

### Requirement: A provider's failure is its own

A provider that throws, whose answer faults, or whose callback on its request's token throws when the
request is cancelled, SHALL be reported through `Failed`, the list going on with what the others
offered; no keystroke SHALL fail for it. An `OperationCanceledException` SHALL be taken as the
request's own cancellation only when the request's token, or the list's for a resolve, was cancelled,
and as the provider's failure otherwise.

#### Scenario: A cancellation that throws

- **WHEN** a provider registers on its request's token a callback that throws, `Co` is typed, and a
  space ends the word
- **THEN** the space is typed, the list closes, and the failure is reported as the
  `AggregateException` cancelling gathered it in

#### Scenario: A provider that gives up by itself

- **WHEN** a provider's answer faults with an `OperationCanceledException` while its request is still
  wanted, or its resolve throws one while the list is open
- **THEN** it is reported through `Failed`, and the list shows what the others offered

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
typed and numbers. The document's words SHALL be read from the lines nearest the caret outward, the
caret's own line around the caret, no more than 50,000 characters for one answer, since the answer is
given before the keystroke returns.

#### Scenario: A C# editor with both

- **WHEN** both are given to a C# editor holding `result = compute();` and `re` is typed on a new line
- **THEN** the list holds `readonly`, `record`, `ref`, `required`, `result` and `return`

#### Scenario: A long file

- **WHEN** the words of 2,000 lines of 100 characters are asked for from the middle line
- **THEN** the words of the lines nearest the caret are offered, and those of the first and the last
  line are not

#### Scenario: A minified line

- **WHEN** the words of one line of 40,000 words are asked for with the caret in its middle
- **THEN** the words beside the caret are offered, and the line's first and last are not

### Requirement: The web completes as .NET does

The engine's twin SHALL score, rank, select and accept exactly as the engine does.

#### Scenario: Seeded sessions over a real file

- **WHEN** 60 seeded sessions of keystrokes run over a real source file on .NET and in the embedded Bun
- **THEN** after every keystroke both show the same list, the same selection, the same line and the
  same caret

#### Scenario: The document's words past what one answer reads

- **WHEN** the document's words are asked for at 24 seeded carets in a document four times longer
  than one answer reads, on .NET and in the embedded Bun
- **THEN** both offer the same words in the same order
