## ADDED Requirements

### Requirement: A family a page names loads on the web as it does on Photon

A family with files that a `Text`'s style names SHALL load on the web when every file of it is a face an
eQuantic build embedded in an assembly the app's deployment carries, or a file the theme's stylesheet
serves, wherever the family comes from: built by hand, from the app's catalog or from a library's, which
the app may name only as text. A page the server renders SHALL declare, in its head, the faces of every
such family its text names that the theme's stylesheet does not. A page the browser builds SHALL declare
such a family the first time the runtime sets or measures text in it, after asking for every file of it
and only when every one is there, as `@font-face` rules with the family's name as a CSS string, unless the
document already has a face of it, asking for each file at `/_equantic/fonts/<assembly>/<resource>`, which
the server SHALL answer with a redirect to the file's content-addressed URL for a face an eQuantic build
embedded in an assembly the app's deployment carries or a file the theme's stylesheet serves, and with 404
for anything else. Every instance of the app SHALL serve the same files, whatever requests it has
answered. A family SHALL be declared once on a page and once in a document. A family one of whose files
cannot be read, is not there or fails to load SHALL fall back whole, whoever declared it, as on Photon. A
page that measured text in a family SHALL measure again when the runtime declares it and when it is taken
back, whether the page named it, with files or without, or reached it through the theme's face, which
closes every stack. A family over a file no eQuantic build embedded as a face, which the theme does not
name, SHALL NOT be served on the web, and the server SHALL say so.

#### Scenario: A family built by hand, on a page the server renders

- **WHEN** a page's `Text` names `new FontFamily("Probe Sans", file)`, over a font the app's build embedded,
  which the theme does not name
- **THEN** the page's head declares the face with its content-addressed URL, and that URL answers the
  file's bytes

#### Scenario: The same family named twice

- **WHEN** two `Text`s of one page name the same family
- **THEN** the page declares each of its files once

#### Scenario: A family the theme declares

- **WHEN** a `Text` names the theme's own text face
- **THEN** the page's head declares nothing more, since the theme's stylesheet does

#### Scenario: Another instance of the app

- **WHEN** one instance of the app renders a page that names such a family
- **THEN** a second instance, which never rendered it, answers the page's URL for the file and the
  file's identity alike

#### Scenario: A file asked for by its identity

- **WHEN** a browser asks for `/_equantic/fonts/<assembly>/<resource>` for a face an eQuantic build
  embedded in an assembly the app's deployment carries, or for a file the theme's stylesheet serves
  wherever it is embedded
- **THEN** the answer redirects to the file's content-addressed URL, and an assembly the deployment does
  not carry, or a resource neither embedded as a face nor served by the theme, answers 404

#### Scenario: A library the app names only as text

- **WHEN** a family names its assembly only as text, a library the app's deployment carries that no
  assembly references in its metadata
- **THEN** its faces are served as any other's: the page declares them, and every instance answers their
  URLs and their identities

#### Scenario: A font no build embedded as a face

- **WHEN** a page names a family over a font a library embedded another way, which the theme does not name
- **THEN** no page declares it, its text falls back on the web, and the server logs why

#### Scenario: A page the browser builds

- **WHEN** the runtime sets text in a family the document has no face of
- **THEN** it asks for every file of the family, declares each once when all of them are there, as a rule
  naming the family as a CSS string, a page that measured text in the family before measures again, and a
  second text in the family declares nothing

#### Scenario: A cut no text uses is missing

- **WHEN** a page the browser builds sets only upright text in a family whose italic file is not there
- **THEN** the runtime declares none of the family, its text falls back whole, as on the page the server
  renders, and the console names the file

#### Scenario: A file that fails to load

- **WHEN** one of a family's files fails to load in the browser, the theme's, the server's or one the
  runtime declared
- **THEN** every rule of the family is taken back, its text falls back whole, the console names the file,
  and a page that measured text in it measures again: one that named it with files or without, and one
  whose text the theme's face set, all of it or what a named family the machine lacks left to it
