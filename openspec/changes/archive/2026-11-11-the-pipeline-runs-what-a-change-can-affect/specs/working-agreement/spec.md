# Spec Delta

## ADDED Requirements

### Requirement: The pipeline runs what a change can affect

CI SHALL classify a pull request's or a merge group's change by the paths it touches, with
`scripts/ci-changes.sh`, whose self-test it runs first. A change whose every path is a document SHALL
run the test groups that read documents and the guards every change runs, and skip the build, the
conformance suite, the runtime's tests, the Mac jobs, the samples and the extension; a change whose
every path is the VS Code extension's SHALL build the extension and run no .NET test; any other change,
and any change the classifier cannot read, SHALL run everything. A push, a tag and a manual run SHALL
run everything. The required checks SHALL report under their names whatever runs, and a push to main
SHALL cancel the run of the push it replaces.

#### Scenario: A pull request that changes only documents

- **WHEN** a pull request changes only Markdown outside the packed and compiled folders, docs/ or openspec/
- **THEN** its run tests the `web` and `rest` groups on both systems, skips the build, conformance, the
  Mac jobs and the samples, and every required check reports success

#### Scenario: A pull request that changes code

- **WHEN** a pull request changes any path the classifier does not know as a document or the extension's
- **THEN** its run is the whole pipeline, as before

#### Scenario: Two merges land in a row

- **WHEN** a second push reaches main while the run of the first is still going
- **THEN** the first run is cancelled and the second runs whole
