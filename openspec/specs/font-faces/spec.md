# font-faces Specification

## Purpose
The faces an app ships: how a family and its files are declared, how an app's own are read from
the files under its `Assets` folder, and how each target loads them, the web by serving and declaring
them and Photon by registering them with each shell's text engine.

## Requirements

### Requirement: A family carries the files it ships

`FontFamily` SHALL name a family and carry its files, each a `FontFile` that names the assembly it is embedded in, its resource, its weight range and whether it is italic. A family with no files SHALL be one the platform supplies. Two families SHALL be equal when their names and their files are.

#### Scenario: A family the platform supplies

- **WHEN** a theme answers `TextFamily` with `new FontFamily("Georgia")`
- **THEN** it is a family with no files, and every target resolves it from the machine as it did before

#### Scenario: A family a library declares

- **WHEN** a library declares `new FontFamily("Roboto", new FontFile("Acme.Theme", "Fonts/Roboto-Variable.ttf", 100, 900))`
- **THEN** the family is "Roboto" with one upright file whose weights run from 100 to 900

### Requirement: An app's own faces are read from their files

A `.ttf` or `.otf` file under the project's `Assets` folder SHALL be embedded in the app's assembly. The SDK SHALL write a catalog with one member per family, and SHALL read each family's files from the files themselves: the family from the `name` table, the weight from `OS/2` or the range from `fvar`, and the italic from `OS/2`. A file the SDK cannot read SHALL fail the build, and so SHALL two files that claim the same weights and style of one family.

#### Scenario: A variable face and its italic

- **WHEN** `Assets/Fonts` holds an upright and an italic file of "Atkinson Hyperlegible Next", each with a `wght` axis from 200 to 800
- **THEN** `Fonts.AtkinsonHyperlegibleNext` names "Atkinson Hyperlegible Next" with two files, 200 to 800 upright and 200 to 800 italic, each embedded in the app's assembly

#### Scenario: A file that is not a font

- **WHEN** `Assets` holds a `.ttf` file that is not an OpenType font
- **THEN** the build fails, naming the file

### Requirement: The web serves the faces and declares them

The server SHALL serve every file of every family the theme names and of every family in the app's catalog. The theme's families are its `TextFamily`, its `MonoFamily` and the family of every role. Each file SHALL be served at `/_equantic/fonts/<hash>.<ext>`, where the hash is taken from the file's content, with `Cache-Control: public, max-age=31536000, immutable`.

The theme's stylesheet SHALL hold one `@font-face` rule per file, with the family, the URL, the weight range, the style and `font-display: swap`. The head SHALL preload the upright files of the theme's `TextFamily`. A theme whose families ship no files SHALL write the head it wrote before. A family one of whose files cannot be read SHALL NOT be served at all, so its text falls back on the web as it does on Photon.

#### Scenario: A brand theme

- **WHEN** the theme's `TextFamily` ships one upright file and one italic file
- **THEN** the head preloads the upright file, the stylesheet declares both, and each URL answers the file's bytes as `font/ttf`

#### Scenario: A family missing one of its files

- **WHEN** a family names an upright file its assembly holds and an italic one it does not
- **THEN** neither file is served or declared, and the missing resource is logged

#### Scenario: A URL nothing names

- **WHEN** a browser asks for `/_equantic/fonts/<hash>.ttf` with a hash no family's file has
- **THEN** the answer is 404

### Requirement: Photon registers a family's files with its text engine

The first time text asks for a family that ships files, each native shell SHALL register the files with its text engine, reading each one from the assembly it is embedded in. It SHALL then measure and draw the text in that face. A family resolves from its files whole or not at all: one with a file that cannot be read or registered SHALL be reported as an unresolved family, and the text SHALL be set in the platform's face, never in the part of the family that registered nor in an installed family of the same name.

#### Scenario: A face the machine never installed

- **WHEN** text is measured in a family whose file names a family installed nowhere
- **THEN** it measures as that face does, not as the system's, and no family is reported unresolved

#### Scenario: A file that is not there

- **WHEN** a family names a resource its assembly does not hold
- **THEN** the family is reported unresolved, and the text is set in the platform's face

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
