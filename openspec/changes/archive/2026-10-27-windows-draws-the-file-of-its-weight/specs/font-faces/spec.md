## MODIFIED Requirements

### Requirement: Photon registers a family's files with its text engine

The first time text asks for a family that ships files, each native shell SHALL register the files with its text engine, reading each one from the assembly it is embedded in. It SHALL then measure and draw the text in that face, whatever name its files give the family. A family resolves from its files whole or not at all: one with a file that cannot be read or registered SHALL be reported as an unresolved family, and the text SHALL be set in the platform's face, never in the part of the family that registered nor in an installed family of the same name.

#### Scenario: A face the machine never installed

- **WHEN** text is measured in a family whose file names a family installed nowhere
- **THEN** it measures as that face does, not as the system's, and no family is reported unresolved

#### Scenario: A family its files call by another name

- **WHEN** a theme names a family "Brand Sans" whose one file's name table calls the family "eQuantic Test
  Face", and Photon on Apple or on Windows measures text in it
- **THEN** it measures as that file does, and no family is reported unresolved

#### Scenario: A file that is not there

- **WHEN** a family names a resource its assembly does not hold
- **THEN** the family is reported unresolved, and the text is set in the platform's face
