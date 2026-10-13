# Spec Delta

## ADDED Requirements

### Requirement: A picture in the README is a capture, taken again when its screen changes

Every picture the root `README.md` shows of a sample SHALL be a capture on record in
`assets/readme/captures.json`, which names the files the pictured screen is written in and the digest
each had when the picture was taken. The test suite SHALL fail when one of those files changes,
naming the picture and the target of `scripts/capture-readme.sh` that takes it again, and SHALL pass
again only when the record holds the files as they are. A picture SHALL be written in the README as
a Markdown image named by its path in the repository, which GitHub draws on every branch and in a
fork. A package's readme SHALL be that README with each picture at its full address under the tag of
the package's own release, written when the package packs, since nuget.org shows a readme's HTML as
text and draws nothing from a path.

#### Scenario: A pictured screen changes

- **WHEN** a file a picture's screen is written in changes
- **THEN** `ReadmeCapturesTests` fails, naming the picture, its target and the file

#### Scenario: The picture was taken again

- **WHEN** the picture is taken again and the files are recorded with `EQ_UPDATE_README_CAPTURES=1`
- **THEN** the test passes

#### Scenario: A picture nobody recorded

- **WHEN** `README.md` shows a picture that has no entry in the record, or the folder holds a file
  the README does not show
- **THEN** the test fails, naming it

#### Scenario: A picture named another way

- **WHEN** `README.md` names a file of `assets/readme` through an `<img>` tag, a link, a full address
  or a path written another way
- **THEN** the test fails, naming it, since GitHub or nuget.org would not draw it

#### Scenario: A package packs

- **WHEN** a package packs
- **THEN** the readme inside it is `README.md` byte for byte, but for each picture's path, which is
  the picture's address under `v<the package's version>`

#### Scenario: A capture of nothing

- **WHEN** a capture holds almost no colour under its status bar
- **THEN** `scripts/capture-readme.sh` refuses it where it is taken
