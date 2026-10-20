# Spec Delta

## ADDED Requirements

### Requirement: A trigger character opens the panel

Typing a character that a signature provider declares in `TriggerCharacters` (`(` by default) SHALL ask
every provider, with the trigger `Character` and that character, as VS Code's `provideSignatureHelp` asks
every provider it holds, and SHALL show the first answer, in the order of `Providers`, that holds a
signature. ⌘⇧Space (Ctrl+Shift+Space) SHALL ask every
provider at the caret, with the trigger `Invoked`. An editor with no signature provider SHALL claim none
of these keys and ask nothing.

#### Scenario: An opening parenthesis

- **WHEN** a provider explains `Column(float gap, MainAlign main)` and `Column(` is typed
- **THEN** the provider was asked once, with the trigger `Character` and `(`, and the panel shows
  `Column(float gap, MainAlign main)` with `float gap` active

#### Scenario: No provider

- **WHEN** an editor with no signature provider is typed `Column(` into, and ⌘⇧Space is pressed
- **THEN** no panel opens and the key is the host's

### Requirement: The panel follows the arguments

While the panel shows, every edit and every move of the caret SHALL ask the providers again, with the
trigger `ContentChange` as a retrigger carrying what shows, after `Delay` (120 ms) without a newer one,
and a provider's `RetriggerCharacters` (`,` by default) SHALL ask at once with the trigger `Character`.
At most one request SHALL be out: a newer one cancels it, and an answer that arrives after a newer
request SHALL be dropped. The panel SHALL show the newest answer's active signature and parameter.

#### Scenario: Walking Column's arguments

- **WHEN** a provider answers the argument the caret is on, and `Column(` then `1, ` then `gap: ` are typed
- **THEN** the active parameter is `float gap`, then `MainAlign main`, then `float gap`

#### Scenario: A burst of keystrokes

- **WHEN** the panel shows and three characters are typed, each within the delay of the one before
- **THEN** one request is made after them, about the text they left

### Requirement: The provider closes the panel

An answer with no signature SHALL close the panel, as SHALL Escape (claimed while the panel shows, after a
completion list and a hover card showing, which it closes first), the editor losing the keyboard, the
document being replaced, and the providers being taken out: with none left, the next request SHALL close
the panel, and a `CodeEditor` handed other providers SHALL close it at once. A press in the code SHALL ask
again rather than close.

#### Scenario: A closing parenthesis

- **WHEN** the panel shows for `Column(` and `)` is typed, and the provider answers nothing outside a call
- **THEN** the panel closes

#### Scenario: Escape

- **WHEN** the panel shows and Escape is pressed
- **THEN** the panel closes, the editor claims the key, and Tab still indents

#### Scenario: Every provider taken out

- **WHEN** the panel shows for `Column(`, every provider is taken out, and `1` is typed
- **THEN** the panel closes

### Requirement: The arrows walk the overloads

With more than one signature, ↑ and ↓ SHALL show the previous and the next signature, round from the last
to the first, while no completion list of more than one entry shows and no hover card hides the panel, as
VS Code's keybindings have it, and ⌥↑ and ⌥↓ (Alt+↑, Alt+↓) SHALL do so beside a list too; with one, or under a card, they SHALL move the caret. A later answer
that holds as many signatures SHALL keep the signature chosen.

#### Scenario: Three overloads

- **WHEN** the panel shows three signatures, the first active, and ↓ is pressed twice and then ↑ three
  times
- **THEN** the active signature is the second, the third, the second, the first and the third

#### Scenario: Beside a list

- **WHEN** the panel shows three signatures, a word typed opens the completion list, and ⌥↓ is pressed
- **THEN** the second signature shows and the list keeps its selection

#### Scenario: Beside a list of one entry

- **WHEN** the panel shows three signatures, a word typed opens a list of one entry, and ↓ is pressed
- **THEN** the second signature shows and the list stays open

#### Scenario: A card over the panel

- **WHEN** the panel shows three signatures, ⌘K ⌘I shows a card, which hides it, and ↑ is pressed
- **THEN** the caret moves up a line, and no other signature was chosen

### Requirement: A parameter is found in its signature's label

A parameter's label SHALL be found in its signature's label in order, each after the one before it and
after the label's first opening parenthesis, and only as a whole word; the found span SHALL be the one
marked when the parameter is active. A label not found SHALL mark nothing.

#### Scenario: Two parameters that begin alike

- **WHEN** a signature `f(int x, int xy)` has the parameters `int x` and `int xy`, and the second is active
- **THEN** the marked span is `int xy`, at columns 9 to 15

#### Scenario: A parameter named like its function

- **WHEN** a signature `x(int x)` has the parameter `x`
- **THEN** the marked span is the parameter's `x`, at column 6, not the function's name

### Requirement: The panel stands over the caret

The editor SHALL draw the panel as a note of its code surface: above the caret's line, its left edge at
the caret and inside the view, and below the line when the view has no room above. With a completion list
showing it SHALL stand on the other side of the line, and SHALL not be drawn when that side has no room. A
hover card showing SHALL hide it. It SHALL show the active signature's label in the code face with the
active parameter's span in the accent colour and, with overloads, which one shows of how many; then the
active parameter's label and documentation; then the signature's documentation. It SHALL be a polite live
region on both targets.

#### Scenario: A call on a line in the middle of the view

- **WHEN** the panel shows for a call on the eleventh line of a view twenty lines tall
- **THEN** the panel's bottom is the top of that line and its left edge is the caret's

#### Scenario: The completion list below the line

- **WHEN** the panel shows and a word typed opens the completion list below the line
- **THEN** both show, the panel above the line and the list below it
