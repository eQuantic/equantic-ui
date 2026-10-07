## ADDED Requirements

### Requirement: A char's search checks its start and its count as .NET does

`IndexOf(char, int)`, `IndexOf(char, int, int)`, `LastIndexOf(char, int)` and
`LastIndexOf(char, int, int)` SHALL search the range their start and count give, ordinally, and SHALL
throw .NET's `ArgumentOutOfRangeException`, with its message, for a start or a count outside the
string, the start checked first. `IndexOf`'s start MAY stand at the end of the string.
`LastIndexOf`'s start SHALL stand on a char of the string, and an empty string's `LastIndexOf` SHALL
answer -1 for any start and count.

#### Scenario: The count ends the search

- **WHEN** `"abcabc".IndexOf('c', 0, 2)` runs
- **THEN** it answers -1

#### Scenario: A start at the end

- **WHEN** `"abcabc".IndexOf('c', 6)` runs
- **THEN** it answers -1

#### Scenario: A start past the end

- **WHEN** `"abc".IndexOf('a', 4)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than or equal to the size of the collection. (Parameter 'startIndex')`

#### Scenario: A count past the end

- **WHEN** `"abcabc".IndexOf('c', 2, 5)` runs
- **THEN** it throws with the message `Count must be positive and count must refer to a location within the string/array/collection. (Parameter 'count')`

#### Scenario: The count ends the search back

- **WHEN** `"abcabc".LastIndexOf('a', 5, 2)` runs
- **THEN** it answers -1

#### Scenario: LastIndexOf from one past the end

- **WHEN** `"abc".LastIndexOf('c', 3)` runs
- **THEN** it throws with the message `Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'startIndex')`

#### Scenario: An empty string

- **WHEN** `"".LastIndexOf('c', 5)` runs
- **THEN** it answers -1
