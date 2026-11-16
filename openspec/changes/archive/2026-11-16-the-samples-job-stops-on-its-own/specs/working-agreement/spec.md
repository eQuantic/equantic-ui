## ADDED Requirements

### Requirement: A hung samples job ends within its limit

The `samples` job SHALL stop at 60 minutes, and its edit-to-view step at 15, so a measurement that
hangs on the macOS runner fails the merge group within a known time rather than holding the runner, and
the queue behind it, until the runner dies.

#### Scenario: The edit-to-view step hangs

- **WHEN** the edit-to-view step does not end
- **THEN** it is stopped 15 minutes after it started, and the job fails with the step's log

#### Scenario: The runner cannot end its own step

- **WHEN** the runner is too starved to stop the step at its limit
- **THEN** GitHub's service stops the job 60 minutes after it started
