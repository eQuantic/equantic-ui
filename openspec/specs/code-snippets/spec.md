# code-snippets Specification

## Purpose
What a code editor does with a snippet, on both targets and with VS Code's keys: it reads the Language Server Protocol's snippet syntax, inserts a copy at every caret as one undo step, walks the stops with Tab and ⇧Tab with every place a stop is written selected, offers a choice as a list, ends the session as VS Code ends it, takes the line's indentation, completes the snippets an app names, and washes the placeholders.

## Requirements

### Requirement: The snippet syntax

`CodeSnippet.Parse` SHALL read the Language Server Protocol's snippet syntax: `$1` and `${1}` a tab stop,
`${1:text}` a placeholder whose text may hold further stops, `${1|one,two|}` a choice whose first option is
its text, `$0` the final stop, and `$NAME`, `${NAME}` and `${NAME:default}` a variable. A stop written more
than once SHALL be one stop, every place holding its first placeholder or choice, the stops nested in that
placeholder included, whichever order they are written in. A backslash
SHALL escape `$`, `}` and `\` in text, and also `,` and `|` in a choice's options. Whatever the syntax does
not make SHALL be text, as typed, and parsing SHALL never fail.

#### Scenario: A loop with mirrors

- **WHEN** `for (var ${1:i} = 0; $1 < ${2:count}; $1++)` is inserted into an empty editor
- **THEN** the text reads `for (var i = 0; i < count; i++)`, and the three `i` are selected, the first the
  primary

#### Scenario: What is not syntax is text

- **WHEN** `cost: $, ${1 and \$2` is inserted
- **THEN** the text reads `cost: $, ${1 and $2`, and no session starts

#### Scenario: A mirror of a placeholder with a stop in it

- **WHEN** `${1:new ${2:Thing}()} = $1` is inserted, Tab pressed and `X` typed
- **THEN** the text reads `new X() = new X()`

#### Scenario: A default that names a later stop

- **WHEN** `${1:$2} ${2:x} $1` is inserted
- **THEN** the text reads `x x x`

#### Scenario: A choice's text

- **WHEN** `${1|one,two\,three|}` is inserted
- **THEN** the text reads `one`, and the options are `one` and `two,three`

### Requirement: Inserting a snippet

`InsertSnippet` SHALL replace every selection with the snippet, a copy at each caret, as one undo step, and
an entry accepted with a snippet SHALL insert it over what it replaces around every caret. A snippet with a
stop other than `$0` SHALL start a session at its first stop; one with `$0` alone, or with no stop, SHALL
leave a caret at `$0`, or at its end, and start none. An editor of one line SHALL NOT insert a snippet whose
text at any caret holds a line break, and SHALL leave its text and its selection as they were.

#### Scenario: A snippet at two carets

- **WHEN** there are carets at the end of `a` and of `b` on two lines, and `(${1:x})` is inserted
- **THEN** the lines read `a(x)` and `b(x)`, both `x` are selected, and one undo gives back `a` and `b`

#### Scenario: The final stop alone

- **WHEN** `Run($0);` is inserted and Tab pressed
- **THEN** the caret stood between the parentheses, and Tab indented there

#### Scenario: A line break in an editor of one line

- **WHEN** an editor of one line reads `abc` and `x\n${1:y}` is inserted at its end
- **THEN** the insertion is refused, the text reads `abc` and no session has started

### Requirement: Tab walks the stops

Tab SHALL select the next stop, in the order of their numbers and then `$0`, or the end of each copy when
the snippet has no `$0`, and reaching that last stop SHALL end the session. ⇧Tab SHALL select the stop
before, and at the first stop SHALL NOT be claimed. A placeholder SHALL be selected when its stop is
reached, and an empty stop SHALL be a caret. A selection heard inside the places of another stop SHALL make
that stop the one Tab and ⇧Tab walk from.

#### Scenario: Three stops and back

- **WHEN** `${1:a} ${2:b} $0.` is inserted, Tab pressed, ⇧Tab pressed, then Tab twice
- **THEN** `b` was selected, then `a`, then `b`, and then a caret stands before the `.` and the session has
  ended

#### Scenario: A press into another placeholder

- **WHEN** `${1:a} ${2:b} ${3:c}` is inserted, a caret placed at the end of `b`, and Tab pressed
- **THEN** `c` is selected

### Requirement: Mirrors are typed with their stop

Every place a stop is written SHALL be selected with it, the others as carets, so what is typed at the stop
is typed at each of them, and two places written side by side SHALL stay apart as they are typed in.

#### Scenario: A mirror

- **WHEN** `${1:name} = $1;` is inserted and `x` typed
- **THEN** the text reads `x = x;`

### Requirement: The active stop grows, the others do not

What is typed at the start or the end of the active stop's text SHALL be in it, and in every placeholder
that place of the stop is nested in, and what is typed at the edge of any other stop SHALL NOT be.

#### Scenario: Typing at the end of a placeholder beside an empty stop

- **WHEN** `${1:a}$2;` is inserted, `x` typed, then `y`, and Tab pressed
- **THEN** the text reads `xy;` and a caret stands after `xy`

#### Scenario: A stop nested in a placeholder at one of its places

- **WHEN** `${2:a} ${1:$2}` is inserted, Tab pressed, `x` and `y` typed, and ⇧Tab pressed
- **THEN** the text reads `xy xy` and the second `xy` is selected

### Requirement: A placeholder typed over takes its nested stops

When Tab or ⇧Tab leaves a stop whose text is not what it was when the stop was reached, every stop nested
in its placeholder SHALL be gone.

#### Scenario: A nested stop

- **WHEN** `${1:new ${2:Thing}()}$0` is inserted and Tab pressed
- **THEN** `Thing` is selected

#### Scenario: A nested stop typed over

- **WHEN** `${1:new ${2:Thing}()}$0` is inserted, `null` typed and Tab pressed
- **THEN** the text reads `null`, a caret stands at its end and the session has ended

### Requirement: A choice is a list

Reaching a stop with choices SHALL show its options in the completion's list at the stop, every option
while the placeholder is selected and those matching what is typed in it after, and accepting one SHALL
write it over every place the stop is written, as one undo step, an empty option included. Leaving the stop
SHALL drop its list.

#### Scenario: Choosing the second option

- **WHEN** `${1|one,two,three|} = $1` is inserted, ↓ pressed and Enter
- **THEN** the list showed `one`, `two` and `three`, and the text reads `two = two`

### Requirement: Variables

`$TM_SELECTED_TEXT` SHALL be the text selected at the copy's caret, `$TM_CURRENT_LINE` the caret's line,
`$TM_CURRENT_WORD` the word at the selection's start, `$TM_LINE_INDEX` the line's index from zero and
`$TM_LINE_NUMBER` from one, every copy reading the document as it stood before any copy went in, whatever
range the copy replaces; a variable with nothing to give SHALL be its default, or nothing. An unknown variable SHALL be a
placeholder holding its name, at a stop after the snippet's own.

#### Scenario: The selected text

- **WHEN** `value` is selected and `Log($TM_SELECTED_TEXT);` inserted
- **THEN** the text reads `Log(value);`

#### Scenario: An unknown variable

- **WHEN** `${AUTHOR} wrote $1` is inserted, and Tab pressed
- **THEN** the text reads `AUTHOR wrote `, the caret stood after `wrote `, and then `AUTHOR` is selected

### Requirement: A snippet takes the line's indentation

Every line of a snippet after its first SHALL start with the indentation of the line it is inserted on,
and a tab at the start of one of its lines SHALL be the language's indent step.

#### Scenario: A block inserted on an indented line

- **WHEN** `if ($1)\n{\n\t$0\n}` is inserted at the end of a line of four spaces, in C#
- **THEN** the lines read `    if ()`, `    {`, eight spaces and `    }`

### Requirement: The session ends

The session SHALL end at its last stop, on Escape, which SHALL leave it before the other carets are taken
away, on an undo or a redo, and when a selection is heard to stand outside every stop.

#### Scenario: Escape

- **WHEN** `${1:a} ${2:b}` is inserted and Escape pressed, then Tab
- **THEN** `a` stays selected, the session has ended, and Tab indented

#### Scenario: A caret moved out

- **WHEN** `(${1:a})` is inserted and End pressed
- **THEN** the session has ended

#### Scenario: An undo

- **WHEN** `${1:a} ${2:b}` is inserted into an empty editor, undone, and Tab pressed
- **THEN** the session has ended, and Tab indented

### Requirement: Named snippets complete

`CodeSnippetCompletionProvider` SHALL offer each `CodeSnippetDefinition` it is given as an entry named by its
prefix, of the snippet kind, its description beside it, which inserts its body as a snippet.

#### Scenario: A loop by its prefix

- **WHEN** an editor is given a provider with a `for` snippet, `fo` is typed and Enter pressed
- **THEN** the snippet is inserted over `fo` and its first stop is selected

### Requirement: The placeholders are drawn

While a session lasts, every placeholder in a view's window SHALL be washed under the text, on the web and on
Photon, and none SHALL be after it ends.

#### Scenario: Two placeholders washed

- **WHEN** `${1:a} ${2:b}` is inserted into an editor
- **THEN** its code block carries a wash over `a` and one over `b`, and none once Escape is pressed
