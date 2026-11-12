## ADDED Requirements

### Requirement: A package's page draws its head

A package's readme SHALL open with a head nuget.org draws: Markdown with no HTML, saying what the head of `README.md` says,
with the same links and pictures. The rest SHALL be `README.md`.

#### Scenario: A pack

- **WHEN** a package is packed
- **THEN** its readme opens with `# eQuantic.UI` and holds no tag of the head

#### Scenario: A head that drifts

- **WHEN** a link, a picture or a word of one head changes and the other does not
- **THEN** a test fails
