## ADDED Requirements

### Requirement: A family a page names loads on the web as it does on Photon

A family with files that a `Text`'s style names SHALL load on the web wherever it comes from: built by
hand, from the app's catalog or from a library's. A page the server renders SHALL declare, in its head,
the faces of every such family its text names that the theme's stylesheet does not, and the server SHALL
serve their files at their content-addressed URLs. A page the browser builds SHALL declare such a family
the first time the runtime sets or measures text in it, unless the document already has a face of it,
asking for each file at `/_equantic/fonts/<assembly>/<resource>`, which the server SHALL answer with a
redirect to the file's content-addressed URL for a resource an eQuantic build embedded as a face in an
assembly the app has loaded, and with 404 for anything else. A family SHALL be declared once on a page and once in a
document. A family one of whose files cannot be read or fails to load SHALL fall back whole, as on Photon.

#### Scenario: A family built by hand, on a page the server renders

- **WHEN** a page's `Text` names `new FontFamily("Probe Sans", file)`, whose file an assembly of the app
  embeds and the theme does not name
- **THEN** the page's head declares the face with its content-addressed URL, and that URL answers the
  file's bytes

#### Scenario: The same family named twice

- **WHEN** two `Text`s of one page name the same family
- **THEN** the page declares each of its files once

#### Scenario: A family the theme declares

- **WHEN** a `Text` names the theme's own text face
- **THEN** the page's head declares nothing more, since the theme's stylesheet does

#### Scenario: A file asked for by its identity

- **WHEN** a browser asks for `/_equantic/fonts/<assembly>/<resource>` for a font resource of an assembly
  the app has loaded
- **THEN** the answer redirects to the file's content-addressed URL, and an assembly the app has not
  loaded, or a resource an eQuantic build did not embed as a face, answers 404

#### Scenario: A page the browser builds

- **WHEN** the runtime sets text in a family the document has no face of
- **THEN** it declares each of the family's files once, and a second text in the family declares nothing

#### Scenario: A file that fails to load

- **WHEN** one of a family's files fails to load in the browser
- **THEN** every face of the family is taken back, its text falls back whole, and the console names the
  file
