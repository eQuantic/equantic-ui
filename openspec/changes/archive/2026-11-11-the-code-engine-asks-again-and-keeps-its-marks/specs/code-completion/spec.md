## MODIFIED Requirements

### Requirement: A list asks once when a word starts, and filters after that

An editor's completion SHALL ask its providers when a word starts (a word character typed where no
list is open, a provider's trigger character, ⌃Space), and SHALL filter and rank what they answered
on every keystroke after that without asking again, unless a provider called its answer incomplete,
which SHALL be asked again whenever the word changes. A complete answer SHALL cover the words that extend
the word it was asked for and no other: once the word typed no longer extends it, its provider SHALL be
asked again for the word as it stands, with the trigger the list opened with (invoked for a list a trigger
character opened), and the providers whose answers the word still extends SHALL NOT be asked.

#### Scenario: A word typed letter by letter

- **WHEN** a provider offers `Column`, `ColorToken`, `Row` and `Collection` and `Col` is typed, then `u`
- **THEN** the provider was asked once, with the trigger `Typing` and the character `C`, the list
  showed `Collection`, `ColorToken`, `Column`, and then `Column` alone

#### Scenario: An incomplete answer

- **WHEN** one provider answers incomplete and another complete, and `Co` is typed
- **THEN** the first is asked again with the trigger `Incomplete`, and the second is not

#### Scenario: Back past the word a complete answer was asked for

- **WHEN** ⌃Space is pressed at the end of `Col`, a provider answers complete by prefix, and Backspace is
  pressed
- **THEN** the provider is asked again for `Co` with the trigger `Invoked`, the entry it offers for `Co`
  shows, and a letter typed after that asks nothing
