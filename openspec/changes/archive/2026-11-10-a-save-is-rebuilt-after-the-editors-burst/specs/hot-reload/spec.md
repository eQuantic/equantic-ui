## ADDED Requirements

### Requirement: A save is rebuilt after the editor's burst, and none is dropped

The server SHALL start a rebuild once the app's sources have been quiet for 50ms, each change of the
same save restarting that wait. A save that arrives while a rebuild runs SHALL be rebuilt when that
rebuild ends, by one more rebuild however many saves arrived.

#### Scenario: Saves during a rebuild

- **WHEN** two saves arrive while a rebuild runs
- **THEN** one more rebuild runs when it ends, so the rebuilds number two, where the server used to
  run one and keep the browser on the earlier edit

#### Scenario: The wait after a save

- **WHEN** `scripts/measure-edit-to-view.cs web` times an edit of the dashboard sample
- **THEN** its "wait" part, from the save to the server starting its rebuild, is under 100ms
