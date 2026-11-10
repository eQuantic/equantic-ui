## ADDED Requirements

### Requirement: A save is compiled by an eqc the server keeps running

The server SHALL ask the project's MSBuild how to start eqc once for a run, as the app starts, and
SHALL keep that eqc running, asking it to compile on each save. No MSBuild SHALL run for a save. The
server SHALL own the process: eqc SHALL end when its standard input does, a stop or a compile past
its limit SHALL kill it, and the next save SHALL start it again.

#### Scenario: A save

- **WHEN** `scripts/measure-edit-to-view.cs web` times an edit of the dashboard sample
- **THEN** its "msbuild" part is a few milliseconds, and eqc's part pays no process start

#### Scenario: eqc died

- **WHEN** the running eqc exits between two saves
- **THEN** the next save starts it again, with what MSBuild said at the start of the run, and compiles

#### Scenario: A compile past its limit, or a stop

- **WHEN** a compile takes longer than its limit, or the app stops while one runs
- **THEN** eqc is killed, and the compile answers that it did not run

#### Scenario: A broken edit, then its fix

- **WHEN** a save does not compile, and the next one does
- **THEN** the first sends its errors to the development overlay, and the second reloads the page with
  the fix, with no restart of the app or of eqc
