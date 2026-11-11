## ADDED Requirements

### Requirement: Asking MSBuild how to start eqc never holds the loop

The MSBuild the server asks how to start eqc SHALL leave no worker node or server behind holding its
streams, and what it wrote SHALL be read within what is left of the time the server gives it, so a save
that asks answers instead of waiting on a stream nothing will close.

#### Scenario: An MSBuild that exits and leaves its output held

- **WHEN** the MSBuild the server asks exits, and something it started goes on holding its output
- **THEN** the asking ends within its limit, and the save answers that eqc could not be started
