## ADDED Requirements

### Requirement: A rebuild's output never holds the loop

A failed rebuild's output SHALL be read within what is left of its limit, and the rebuild SHALL leave no worker node or
server behind holding its streams.

#### Scenario: A failed rebuild that left its output held

- **WHEN** a rebuild exits with an error, and something it started goes on holding its output
- **THEN** the rebuild ends within its limit, and the next one can start
