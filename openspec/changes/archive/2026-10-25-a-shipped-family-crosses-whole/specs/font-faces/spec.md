## ADDED Requirements

### Requirement: A family is the same family on both sides of hydration

The theme bridge SHALL carry a family the theme ships with its files, for the text face, the code face and
every role, and the browser SHALL rebuild them, so a family a component reads after hydration SHALL be the
one it read on the server, its files and its equality included. A family with no files SHALL cross as its
name alone. A file's names SHALL be written so that no name can close the script element the payload is
written into.

#### Scenario: A shipped text face crosses with its files

- **WHEN** a theme's `TextFamily` is "Brand Sans" with an upright file and an italic file
- **THEN** after hydration `TextFamily.Files` holds the same two files, with the same assembly, resource,
  weights and slant

#### Scenario: A family with no files

- **WHEN** a theme's `TextFamily` is "Georgia", which ships no file
- **THEN** the payload carries the name alone, as it did before families carried files

#### Scenario: A file name that would close the script element

- **WHEN** a file's resource name holds `</script>`
- **THEN** the payload holds no `<`, and the browser reads the name back as it was written

### Requirement: A face a shell opens from a file is cached whole

A shell whose text engine opens a face only from a file SHALL write it to its cache whole or not at all,
under a name derived from its bytes. A cache that cannot take the face SHALL be a face that did not
register, so its family falls back and is reported, and SHALL throw nothing where text is measured.

#### Scenario: A cache that cannot take a face

- **WHEN** the cache's folder cannot be created
- **THEN** the face does not register, nothing throws, and its family falls back

#### Scenario: A write that fails

- **WHEN** writing a face fails once it has begun
- **THEN** nothing is left under the face's name, and nothing beside it
