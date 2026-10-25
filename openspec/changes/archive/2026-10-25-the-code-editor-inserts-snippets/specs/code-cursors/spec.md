## MODIFIED Requirements

### Requirement: Escape goes back to one caret

Escape SHALL close the completion list, then the card, then the signature panel, then leave a snippet's
session, then take away every caret but the primary, and only then release Tab, one at a time.

#### Scenario: Escape with three carets

- **WHEN** there are three carets and nothing shows over the code, and Escape is pressed
- **THEN** the primary alone is left, the key is claimed, and Tab still indents

#### Scenario: Escape in a snippet with a mirror

- **WHEN** `${1:a} = $1` is inserted and Escape pressed twice
- **THEN** the first Escape ends the session and leaves both carets, and the second leaves the primary alone
