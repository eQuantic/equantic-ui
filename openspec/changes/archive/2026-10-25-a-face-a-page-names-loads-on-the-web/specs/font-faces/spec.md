## ADDED Requirements

### Requirement: A family a page names loads on the web as it does on Photon

A family with files that a `Text`'s style names SHALL load on the web when every file of it is a face an
eQuantic build embedded in the app's assemblies, or a file the theme's stylesheet serves, wherever the
family comes from: built by hand, from the app's catalog or from a library's. A page the server renders
SHALL declare, in its head, the faces of every such family its text names that the theme's stylesheet does
not. A page the browser builds SHALL declare such a family the first time the runtime sets or measures
text in it, as `@font-face` rules with the family's name as a CSS string, unless the document already has
a face of it, asking for each file at `/_equantic/fonts/<assembly>/<resource>`, which the server SHALL
answer with a redirect to the file's content-addressed URL for a face an eQuantic build embedded in the
app's assemblies, and with 404 for anything else. Every instance of the app SHALL serve the same files,
whatever requests it has answered. A family SHALL be declared once on a page and once in a document. A
family one of whose files cannot be read or fails to load SHALL fall back whole, whoever declared it, as on
Photon, and a page that measured text in it SHALL measure again. A family over a file no eQuantic build
embedded as a face, which the theme does not name, SHALL NOT be served on the web, and the server SHALL
say so.

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
  embedded in an assembly of the app
- **THEN** the answer redirects to the file's content-addressed URL, and an assembly that is not the
  app's, or a resource no eQuantic build embedded as a face, answers 404

#### Scenario: A font no build embedded as a face

- **WHEN** a page names a family over a font a library embedded another way, which the theme does not name
- **THEN** no page declares it, its text falls back on the web, and the server logs why

#### Scenario: A page the browser builds

- **WHEN** the runtime sets text in a family the document has no face of
- **THEN** it declares each of the family's files once, as a rule naming the family as a CSS string, and a
  second text in the family declares nothing

#### Scenario: A file that fails to load

- **WHEN** one of a family's files fails to load in the browser, the theme's, the server's or one the
  runtime declared
- **THEN** every rule of the family is taken back, its text falls back whole, the console names the file,
  and a page that measured text in it measures again
