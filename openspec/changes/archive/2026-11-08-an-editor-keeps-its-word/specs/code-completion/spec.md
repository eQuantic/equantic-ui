## ADDED Requirements

### Requirement: A complete answer holds for the words that extend its word

A complete answer SHALL be kept while the word extends the word it was asked for, and its provider SHALL
be asked again, as `Invoked`, once the word no longer does. A provider whose answer still holds SHALL NOT
be asked.

#### Scenario: The word taken back past the answer

- **WHEN** the list is opened on `Col` and Backspace takes the word to `Co`
- **THEN** the provider is asked again as `Invoked`, and what it offers for `Co` is listed

#### Scenario: Back at the answer's own word

- **WHEN** the list is opened on `Co`, `l` is typed, and Backspace takes the word back to `Co`
- **THEN** the provider is not asked again
