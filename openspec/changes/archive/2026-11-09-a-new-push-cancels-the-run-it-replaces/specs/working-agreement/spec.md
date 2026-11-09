## ADDED Requirements

### Requirement: A new push to a pull request cancels the run it replaces

CI SHALL cancel a pull request's run in progress when the same pull request starts a new one, and
SHALL never cancel, nor queue behind another, a run on main, on a tag, on a merge group or started by
hand.

#### Scenario: Two pushes to a pull request

- **WHEN** a pull request gets a second push while the run of the first is still going
- **THEN** the first run is cancelled and the second one runs

#### Scenario: A push to main while pull requests run

- **WHEN** main gets a push while a pull request's runs come and go
- **THEN** main's run goes to the end
