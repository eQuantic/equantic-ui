## MODIFIED Requirements

### Requirement: Accepting writes over the word typed, as one step

Accepting SHALL replace the word typed since the list opened, or the provider's own range, carried through
every edit made since the provider was asked, at the caret and at every other caret, what is typed at its
start or end being in it, leave the caret after the text, and be one undo step. An entry that holds a
snippet SHALL insert it there, and start its session (see `code-snippets`).

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

#### Scenario: An entry that inserts a snippet

- **WHEN** a provider offers `Console.WriteLine` with the snippet `Console.WriteLine($1);$0`, `Con` is typed
  and Enter pressed
- **THEN** the line reads `Console.WriteLine();` with the caret between the parentheses, and Tab takes it
  after the semicolon

### Requirement: One entry is listed once

Offers of one label and one inserted text SHALL be listed once, as the first provider's copy that
matches the word, so that a copy that does not match hides none that does. An offer that inserts a
snippet SHALL be another entry than one that inserts text, whatever the text reads.

#### Scenario: Two providers offering Column

- **WHEN** the first provider's `Column` filters by `zzz`, the second's by its label, and `Col` is typed
- **THEN** `Column` is listed once, the second provider's

#### Scenario: A snippet named like a keyword

- **WHEN** an editor is given the keywords and a snippet named `for`, and `for` is typed
- **THEN** the keyword `for` and the snippet `for` are both listed
