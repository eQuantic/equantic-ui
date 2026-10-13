# Spec Delta

## ADDED Requirements

### Requirement: A picture in the README is a capture, taken again when its screen changes

Every picture the root `README.md` shows of a sample SHALL be a capture on record in
`assets/readme/captures.json`, which names the files the pictured screen is written in and the digest
each had when the picture was taken. The test suite SHALL fail when one of those files changes,
naming the picture and the target of `scripts/capture-readme.sh` that takes it again, and SHALL pass
again only when the record holds the files as they are. A picture SHALL be written in the README as
a Markdown image named by its full address, since the README is also every package's readme on
nuget.org, which shows HTML as text and draws nothing from a relative path.

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

#### Scenario: A relative path

- **WHEN** `README.md` names a picture by a path relative to the repository
- **THEN** the test fails, since nuget.org draws nothing from it

#### Scenario: An HTML image

- **WHEN** `README.md` shows a picture through an `<img>` tag
- **THEN** the test fails, since nuget.org shows the tag as text

#### Scenario: A capture of nothing

- **WHEN** a capture holds almost no colour under its status bar
- **THEN** `scripts/capture-readme.sh` refuses it where it is taken
