## ADDED Requirements

### Requirement: A served eqc compiles again only the files a save can have changed

A build of the eqc the server keeps running SHALL compile again only the files whose text changed,
the files whose last compile read one of them, and the files whose last compile failed, when the text
outside every body of each changed file is the same as before the save. It SHALL compile every file
when the text outside a body changed. Every module it writes SHALL be byte for byte what a cold build
writes after the same edit.

#### Scenario: A body of one screen changed

- **WHEN** a save changes a text literal inside one component's `Build`
- **THEN** the build compiles that file again and not the files that read nothing of it, and every
  module is what a cold build writes

#### Scenario: A body another file's compile reads changed

- **WHEN** a save changes the body of a default interface member, or of a struct method another
  file's component calls, declared in a file of its own
- **THEN** the build compiles again the file that read that body, and its module is what a cold
  build writes

#### Scenario: A declaration changed

- **WHEN** a save changes a constant, or a component's constructor that the generated factory calls
- **THEN** the build compiles every file, and the modules that read the constant or call the factory
  are what a cold build writes

#### Scenario: A file not compiled again still reads its strings

- **WHEN** a file that reads a resource string is not compiled again
- **THEN** the culture catalogs the build writes still hold that string
